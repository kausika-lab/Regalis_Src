using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Domain.Enums;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// The quotation feature (specification §1–§5). The arithmetic case below is the
/// worked example the document supplies for exactly this purpose (§5.2); the rest
/// covers the rules it calls the most important ones — a sent quotation is frozen,
/// new prices go out as a revision, and only the newest revision can be approved.
/// </summary>
[Collection(ApiCollection.Name)]
public class QuotationTests(RegalisApiFactory factory)
{
    private async Task<(HttpClient Admin, HttpClient Customer, long CustomerId)> SetUpAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);
        return (admin, customer, session.User.UserId);
    }

    private static SaveQuotationRequest WorkedExample(long customerId) => new(
        CustomerId: customerId,
        LocationId: 1,
        QuotationDate: null,
        ValidUntil: null,
        ReferenceNumber: "PO-4471",
        Subject: "Worked example from the specification",
        SalespersonId: null,
        // Karnataka: intra-state, so the GST splits CGST + SGST.
        PlaceOfSupplyStateCode: "29",
        DiscountType: (byte)QuotationDiscountType.Percentage,
        DiscountValue: 5m,
        TdsCode: null,
        AdjustmentLabel: null,
        AdjustmentAmount: null,
        AdvanceInvoiceEnabled: null,
        AdvanceType: null,
        AdvanceValue: null,
        TermsAndConditions: null,
        Remarks: null,
        InternalNote: "Not for the customer.",
        Lines:
        [
            Line("Cabin C-001", QuotationServiceType.Cabin, 1m, 3m, "Month", 25000m),
            Line("Dedicated desk", QuotationServiceType.Desk, 5m, 1m, "Month", 6000m),
            Line("Meeting Room A", QuotationServiceType.MeetingRoom, 1m, 4m, "Hour", 800m),
        ]);

    private static SaveQuotationLineRequest Line(string description,
        QuotationServiceType serviceType, decimal quantity, decimal duration, string unit,
        decimal rate) => new(
        LineType: (byte)QuotationLineType.Service,
        ServiceType: (byte)serviceType,
        WorkspaceTypeId: null, ServiceItemId: null, WorkspaceId: null, PassPlanId: null,
        Description: description, LineNotes: null, HsnSacCode: null,
        Quantity: quantity, DurationValue: duration, DurationUnit: unit,
        UnitPrice: rate, StandardRate: null, DiscountAmount: 0m, TaxPercent: 18m);

    private static async Task<QuotationDto> CreateAsync(HttpClient admin,
        SaveQuotationRequest request)
    {
        var response = await admin.PostAsJsonAsync("/api/quotations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;
    }

    private static async Task<QuotationDto> SendAsync(HttpClient admin, long quotationId)
    {
        // No SMTP is configured under test, which the dispatcher reports as "not sent"
        // rather than as a failure, so the quotation still reaches Sent.
        var response = await admin.PostAsJsonAsync($"/api/quotations/{quotationId}/send",
            new SendQuotationRequest(true, false, "buyer@example.com", null, false,
                null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;
    }

    [Fact]
    public async Task WorkedExample_MatchesTheSpecificationToThePaisa()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var quotation = await CreateAsync(admin, WorkedExample(customerId));

        quotation.SubTotal.Should().Be(108200.00m);
        quotation.DiscountAmount.Should().Be(5410.00m);
        quotation.TaxableAmount.Should().Be(102790.00m);
        quotation.CgstAmount.Should().Be(9251.10m);
        quotation.SgstAmount.Should().Be(9251.10m);
        quotation.IgstAmount.Should().Be(0m);
        // 1,02,790.00 + 18,502.20 = 1,21,292.20, rounded down to the rupee.
        quotation.RoundOffAmount.Should().Be(-0.20m);
        quotation.TotalAmount.Should().Be(121292.00m);
        quotation.TaxMode.Should().Be((byte)QuotationTaxMode.IntraState);

        // The discount is split in proportion to line value, and the shares add back
        // to exactly the discount.
        quotation.Lines.Select(l => l.DiscountShare)
            .Should().Equal(3750.00m, 1500.00m, 160.00m);
        quotation.Lines.Select(l => l.TaxableAmount)
            .Should().Equal(71250.00m, 28500.00m, 3040.00m);
        quotation.Lines.Select(l => l.TaxAmount)
            .Should().Equal(12825.00m, 5130.00m, 547.20m);
        quotation.Lines.Sum(l => l.DiscountShare).Should().Be(quotation.DiscountAmount);

        quotation.TotalInWords.Should()
            .Be("Rupees One Lakh Twenty One Thousand Two Hundred Ninety Two Only");
    }

    [Fact]
    public async Task OutsideKarnataka_ChargesIgstInsteadOfCgstAndSgst()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var quotation = await CreateAsync(admin,
            WorkedExample(customerId) with { PlaceOfSupplyStateCode = "27" });

        quotation.TaxMode.Should().Be((byte)QuotationTaxMode.InterState);
        quotation.IgstAmount.Should().Be(18502.20m);
        quotation.CgstAmount.Should().Be(0m);
        quotation.SgstAmount.Should().Be(0m);
        // The tax charged is the same either way; only its split differs.
        quotation.TotalAmount.Should().Be(121292.00m);
    }

    [Fact]
    public async Task HeaderLine_CarriesNoMoney()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var request = WorkedExample(customerId) with
        {
            Lines =
            [
                new SaveQuotationLineRequest((byte)QuotationLineType.Header, null, null, null,
                    null, null, "Workspace", null, null, 0m, null, null, 0m, null, 0m, null),
                Line("Cabin C-001", QuotationServiceType.Cabin, 1m, 1m, "Month", 1000m),
            ],
            DiscountValue = 0m,
        };

        var quotation = await CreateAsync(admin, request);

        quotation.SubTotal.Should().Be(1000.00m);
        quotation.Lines.Single(l => l.LineType == (byte)QuotationLineType.Header)
            .LineAmount.Should().Be(0m);
    }

    [Fact]
    public async Task SentQuotation_IsFrozenAgainstEditing()
    {
        var (admin, _, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));

        var sent = await SendAsync(admin, quotation.QuotationId);
        sent.Status.Should().Be((byte)QuotationStatus.Sent);

        var edit = await admin.PutAsJsonAsync($"/api/quotations/{quotation.QuotationId}",
            WorkedExample(customerId));
        edit.IsSuccessStatusCode.Should().BeFalse();

        var delete = await admin.DeleteAsync($"/api/quotations/{quotation.QuotationId}");
        delete.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Revising_OpensANewDraftAndRetiresTheOriginal()
    {
        var (admin, _, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));
        await SendAsync(admin, quotation.QuotationId);

        var response = await admin.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/revise",
            new ReviseQuotationRequest("Customer asked for a lower rate"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var revision = (await response.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;

        revision.QuotationNumber.Should().Be($"{quotation.QuotationNumber}-R1");
        revision.RevisionNumber.Should().Be(1);
        revision.Status.Should().Be((byte)QuotationStatus.Draft);
        revision.TotalAmount.Should().Be(quotation.TotalAmount);

        var original = (await admin.GetFromJsonAsync<ApiResponse<QuotationDto>>(
            $"/api/quotations/{quotation.QuotationId}"))!.Data!;
        original.Status.Should().Be((byte)QuotationStatus.Revised);
        original.ReplacedByQuotationNumber.Should().Be(revision.QuotationNumber);

        // Revising twice, or deciding on a replaced quotation, are both refused.
        (await admin.PostAsJsonAsync($"/api/quotations/{quotation.QuotationId}/revise",
            new ReviseQuotationRequest(null))).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Customer_SeesOnlyTheirOwnAndNeverADraftOrAnInternalNote()
    {
        var (admin, customer, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));

        // A draft is not the customer's business — and the id must not leak that it exists.
        (await customer.GetAsync($"/api/quotations/my/{quotation.QuotationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await SendAsync(admin, quotation.QuotationId);

        var mine = (await customer.GetFromJsonAsync<ApiResponse<QuotationDto>>(
            $"/api/quotations/my/{quotation.QuotationId}"))!.Data!;
        // Opening a sent quotation is what "Viewed" means.
        mine.Status.Should().Be((byte)QuotationStatus.Viewed);
        mine.InternalNote.Should().BeNull();

        // Another customer's quotation is a 404, not a 403.
        var other = factory.CreateClient();
        var (_, _, otherSession) = await other.RegisterCustomerAsync(factory);
        other.UseToken(otherSession.AccessToken);
        (await other.GetAsync($"/api/quotations/my/{quotation.QuotationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApprovalNeedsTheTermsTicked_AndThenConvertsToAnInvoiceOnce()
    {
        var (admin, customer, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));
        await SendAsync(admin, quotation.QuotationId);

        var withoutTerms = await customer.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/approve",
            new QuotationDecisionRequest(null, null, false));
        withoutTerms.IsSuccessStatusCode.Should().BeFalse();

        var approved = await customer.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/approve",
            new QuotationDecisionRequest(null, null, true));
        approved.EnsureSuccessStatusCode();
        var decided = (await approved.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;
        decided.Status.Should().Be((byte)QuotationStatus.Approved);
        decided.DecisionOnBehalf.Should().BeFalse();

        var invoiced = await admin.PostAsync(
            $"/api/invoices/from-quotation/{quotation.QuotationId}", null);
        invoiced.EnsureSuccessStatusCode();
        var invoice = (await invoiced.Content.ReadFromJsonAsync<ApiResponse<InvoiceDto>>())!.Data!;
        // The customer approved these figures; the invoice carries them unchanged.
        invoice.TotalAmount.Should().Be(decided.TotalAmount);

        // Rule 5: only once.
        (await admin.PostAsync($"/api/invoices/from-quotation/{quotation.QuotationId}", null))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Rejecting_WithoutSayingWhyOnOther_IsRefused()
    {
        var (admin, customer, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));
        await SendAsync(admin, quotation.QuotationId);

        var vague = await customer.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/reject",
            new QuotationDecisionRequest("OTHER", null, false));
        vague.IsSuccessStatusCode.Should().BeFalse();

        var response = await customer.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/reject",
            new QuotationDecisionRequest("PRICE", "Above our budget", false));
        response.EnsureSuccessStatusCode();
        var rejected = (await response.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;
        rejected.Status.Should().Be((byte)QuotationStatus.Rejected);
        rejected.RejectionCode.Should().Be("PRICE");
    }

    [Fact]
    public async Task AdminCanRecordADecisionTakenOverThePhone()
    {
        var (admin, _, customerId) = await SetUpAsync();
        var quotation = await CreateAsync(admin, WorkedExample(customerId));
        await SendAsync(admin, quotation.QuotationId);

        var response = await admin.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/record-decision",
            new RecordQuotationDecisionRequest(true, (byte)QuotationDecisionMode.Phone,
                "Ravi Kumar", "Confirmed on a call", null));
        response.EnsureSuccessStatusCode();
        var decided = (await response.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;

        decided.Status.Should().Be((byte)QuotationStatus.Approved);
        // The record says plainly that this was not the customer's own click.
        decided.DecisionOnBehalf.Should().BeTrue();
        decided.DecisionConfirmedBy.Should().Be("Ravi Kumar");
        decided.DecisionMode.Should().Be((byte)QuotationDecisionMode.Phone);
    }

    [Fact]
    public async Task Summary_CountsTheFinancialYearAndEveryStatus()
    {
        var (admin, _, customerId) = await SetUpAsync();
        var before = (await admin.GetFromJsonAsync<ApiResponse<QuotationSummaryDto>>(
            "/api/quotations/summary"))!.Data!;

        await CreateAsync(admin, WorkedExample(customerId));

        var after = (await admin.GetFromJsonAsync<ApiResponse<QuotationSummaryDto>>(
            "/api/quotations/summary"))!.Data!;

        after.TotalCount.Should().Be(before.TotalCount + 1);
        after.TotalValue.Should().Be(before.TotalValue + 121292.00m);
        after.StatusCounts[(byte)QuotationStatus.Draft]
            .Should().Be(before.StatusCounts[(byte)QuotationStatus.Draft] + 1);
        after.FinancialYear.Should().MatchRegex(@"^\d{4}-\d{2}$");
    }

    [Fact]
    public async Task FormOptions_OfferTheConfiguredServicesAndTaxChoices()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var options = (await admin.GetFromJsonAsync<ApiResponse<QuotationFormOptionsDto>>(
            "/api/quotations/form-options"))!.Data!;

        options.NextQuotationNumber.Should().Contain("/QTN/");
        options.DefaultGstPercent.Should().Be(18m);
        options.GstRateOptions.Should().Contain(18m);
        options.HomeStateCode.Should().Be("29");
        options.States.Should().Contain(s => s.Code == "29" && s.Name == "Karnataka");
        options.TdsOptions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TypedInCustomer_NeedsNoAccount_ButCannotBeInvoiced()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var quotation = await CreateAsync(admin, WorkedExample(customerId) with
        {
            CustomerId = null,
            LocationId = null,
            CustomerName = "Acme Prospects LLP",
            BillingAddress = "12 MG Road\nBengaluru 560001",
            CustomerGstin = "29abcde1234f1z5",
        });

        quotation.CustomerId.Should().BeNull();
        quotation.CustomerName.Should().Be("Acme Prospects LLP");
        quotation.BillingAddress.Should().Be("12 MG Road\nBengaluru 560001");
        quotation.CustomerGstin.Should().Be("29ABCDE1234F1Z5");
        // No location on the form any more: the default one is taken.
        quotation.LocationId.Should().BeGreaterThan(0);

        var sent = await SendAsync(admin, quotation.QuotationId);
        sent.Status.Should().Be((byte)QuotationStatus.Sent);
        // The send dialog is the only place a prospect's email was ever given.
        sent.CustomerEmail.Should().Be("buyer@example.com");
        sent.CustomerName.Should().Be("Acme Prospects LLP");
        sent.Actions.CanCopyPortalLink.Should().BeFalse();

        var decided = await admin.PostAsJsonAsync(
            $"/api/quotations/{quotation.QuotationId}/record-decision",
            new RecordQuotationDecisionRequest(true, (byte)QuotationDecisionMode.Email,
                "Acme purchasing", null, null));
        decided.EnsureSuccessStatusCode();
        var approved = (await decided.Content.ReadFromJsonAsync<ApiResponse<QuotationDto>>())!.Data!;
        approved.Actions.CanConvertToInvoice.Should().BeFalse();

        (await admin.PostAsync($"/api/invoices/from-quotation/{quotation.QuotationId}", null))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task TypedDetails_WinOverTheLinkedAccount()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var quotation = await CreateAsync(admin, WorkedExample(customerId) with
        {
            CustomerName = "Registered Name Pvt Ltd",
            BillingAddress = "Site office, Whitefield",
        });

        quotation.CustomerId.Should().Be(customerId);
        quotation.CustomerName.Should().Be("Registered Name Pvt Ltd");
        quotation.BillingAddress.Should().Be("Site office, Whitefield");

        var sent = await SendAsync(admin, quotation.QuotationId);
        sent.CustomerName.Should().Be("Registered Name Pvt Ltd");
        sent.BillingAddress.Should().Be("Site office, Whitefield");
        sent.Actions.CanCopyPortalLink.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Acme", "NOT-A-GSTIN")]
    public async Task CustomerWithoutAName_OrWithABadGstin_IsRefused(string? name, string? gstin)
    {
        var (admin, _, customerId) = await SetUpAsync();

        var response = await admin.PostAsJsonAsync("/api/quotations",
            WorkedExample(customerId) with
            {
                CustomerId = null,
                CustomerName = name,
                CustomerGstin = gstin,
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DateRange_IsInclusive_DrivesTheTabCounts_AndTheListSorts()
    {
        var (admin, _, customerId) = await SetUpAsync();
        // Three quotations for a fresh customer, so the filter isolates them.
        foreach (var (day, discount) in new[] { (1, 0m), (15, 10m), (30, 5m) })
        {
            await CreateAsync(admin, WorkedExample(customerId) with
            {
                QuotationDate = new DateOnly(2026, 9, day),
                ValidUntil = new DateOnly(2026, 10, 31),
                DiscountValue = discount,
            });
        }
        var scope = $"customerId={customerId}&from=2026-09-01&to=2026-09-15";

        var list = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<QuotationDto>>>(
            $"/api/quotations?{scope}"))!.Data!;
        list.TotalCount.Should().Be(2, "both the From and the To day are included");
        list.Items.Select(q => q.QuotationDate).Should().BeInDescendingOrder();

        var counts = (await admin.GetFromJsonAsync<ApiResponse<QuotationStatusCountsDto>>(
            $"/api/quotations/status-counts?{scope}&status=4"))!.Data!;
        counts.TotalCount.Should().Be(2, "the tab counts ignore the selected tab");
        counts.StatusCounts[(byte)QuotationStatus.Draft].Should().Be(2);

        var byAmount = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<QuotationDto>>>(
            $"/api/quotations?customerId={customerId}&sortBy=amount&sortDir=asc"))!.Data!;
        byAmount.Items.Select(q => q.TotalAmount).Should().BeInAscendingOrder();
        byAmount.TotalCount.Should().Be(3);

        (await admin.GetAsync($"/api/quotations?customerId={customerId}&from=2026-09-15&to=2026-09-01"))
            .IsSuccessStatusCode.Should().BeFalse("To cannot be before From");
    }

    [Fact]
    public async Task ItemMasterLines_RoundTrip_NonTaxableChargesNothing_AndRatesAreRecalled()
    {
        var (admin, _, customerId) = await SetUpAsync();
        var code = $"QI-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var itemResponse = await admin.PostAsJsonAsync("/api/inventory/items",
            new SaveInventoryItemRequest(code, "12U Network Rack", "IT", "Nos", 0m, 0m, 6000m, 1,
                "PDU, tray, fan", (byte)InventoryItemType.Goods, "847330", 8600m, 18m, false));
        itemResponse.EnsureSuccessStatusCode();
        var item = (await itemResponse.Content.ReadFromJsonAsync<ApiResponse<InventoryItemDto>>())!.Data!;

        SaveQuotationLineRequest ItemLine(decimal rate, bool nonTaxable) => new(
            (byte)QuotationLineType.Custom, (byte)QuotationServiceType.Other,
            null, null, null, null, "12U Network Rack", "PDU, tray, fan, cable manager", "847330",
            1m, null, null, rate, null, 0m, 18m,
            item.InventoryItemId, (byte)InventoryItemType.Goods, "Nos", nonTaxable);

        var quotation = await CreateAsync(admin, WorkedExample(customerId) with
        {
            DiscountValue = 0m,
            Lines = [ItemLine(8600m, false), ItemLine(100m, true)],
        });
        var line = quotation.Lines[0];
        line.InventoryItemId.Should().Be(item.InventoryItemId);
        line.ItemType.Should().Be((byte)InventoryItemType.Goods);
        line.Unit.Should().Be("Nos");
        line.LineNotes.Should().Be("PDU, tray, fan, cable manager");
        line.TaxAmount.Should().Be(1548m, "18% of 8,600");
        quotation.Lines[1].IsNonTaxable.Should().BeTrue();
        quotation.Lines[1].TaxAmount.Should().Be(0m, "Non-Taxable ignores the rate sent with it");

        // A draft was never offered; once sent, its rate is recalled.
        (await admin.GetFromJsonAsync<ApiResponse<List<QuotationItemRateDto>>>(
            $"/api/quotations/item-rates?inventoryItemId={item.InventoryItemId}"))!.Data!
            .Should().BeEmpty();
        await SendAsync(admin, quotation.QuotationId);
        var rates = (await admin.GetFromJsonAsync<ApiResponse<List<QuotationItemRateDto>>>(
            $"/api/quotations/item-rates?inventoryItemId={item.InventoryItemId}"))!.Data!;
        rates.Select(r => r.UnitPrice).Should().BeEquivalentTo([8600m, 100m]);

        var unknown = await admin.PostAsJsonAsync("/api/quotations", WorkedExample(customerId) with
        {
            Lines = [ItemLine(1m, false) with { InventoryItemId = int.MaxValue }],
        });
        unknown.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task ServiceLines_PrintTheSpecificationsLineText_AndAmounts()
    {
        var (admin, _, customerId) = await SetUpAsync();
        SaveQuotationLineRequest Svc(QuotationServiceType type, string name, decimal qty,
            decimal? duration, string unit, decimal rate, decimal? standard = null) => new(
            type == QuotationServiceType.Other ? (byte)QuotationLineType.Custom : (byte)QuotationLineType.Service,
            (byte)type, null, null, null, null, name, null, null,
            qty, duration, unit, rate, standard ?? rate, 0m, 18m);

        var quotation = await CreateAsync(admin, WorkedExample(customerId) with
        {
            DiscountValue = 0m,
            Lines =
            [
                Svc(QuotationServiceType.Cabin, "Cabin C-001", 1m, 3m, "Month", 25000m, 27000m),
                Svc(QuotationServiceType.Desk, "Dedicated desk", 5m, 1m, "Month", 6000m),
                Svc(QuotationServiceType.MeetingRoom, "Meeting Room A", 1m, 4m, "Hour", 800m),
                Svc(QuotationServiceType.Pass, "10-Day Pack", 1m, null, "One-time", 6800m),
                Svc(QuotationServiceType.Other, "Setup / branding charges", 1m, null, "One-time", 5000m),
            ],
        });

        quotation.Lines.Select(l => l.Summary).Should().Equal(
            "Cabin C-001 · 1 × 3 Month × ₹25,000",
            "Dedicated desk · 5 × 1 Month × ₹6,000",
            "Meeting Room A · 1 × 4 Hour × ₹800",
            "10-Day Pack · 1 × One-time × ₹6,800",
            "Setup / branding charges · 1 × One-time × ₹5,000");
        quotation.Lines.Select(l => l.LineAmount).Should().Equal(75000m, 30000m, 3200m, 6800m, 5000m);
        quotation.Lines[0].RateEdited.Should().BeTrue("25,000 was quoted against a configured 27,000");
        quotation.Lines[1].RateEdited.Should().BeFalse();
        quotation.SubTotal.Should().Be(120000m);
        (quotation.CgstAmount + quotation.SgstAmount).Should().Be(21600m);
        quotation.TotalAmount.Should().Be(141600m);
    }

    [Fact]
    public async Task DiscountLargerThanTheSubTotal_IsRefused()
    {
        var (admin, _, customerId) = await SetUpAsync();

        var response = await admin.PostAsJsonAsync("/api/quotations",
            WorkedExample(customerId) with
            {
                DiscountType = (byte)QuotationDiscountType.Amount,
                DiscountValue = 999999m,
            });

        response.IsSuccessStatusCode.Should().BeFalse();
    }
}
