using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.API.Controllers;
using Regalis.Application.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>The purchase-order module end to end: server-side pricing, the
/// Draft → PendingApproval → Approved → Sent → Received workflow, the freeze after
/// submit, attachments, the PDF and the audit trail.</summary>
[Collection(ApiCollection.Name)]
public class PurchaseOrderWorkflowTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<(int SupplierId, int LocationId)> SupplierAndLocationAsync(HttpClient admin,
        string gstinState = "29")
    {
        var code = $"SUP{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var created = await admin.PostAsJsonAsync("/api/suppliers", new SaveSupplierRequest(
            code, $"Nandi Coffee Traders {code}", "Accounts", "accounts@nandicoffee.in", "9845022110",
            $"{gstinState}AABCT1234K1ZP", "44, 3rd Main, Peenya Industrial Area", "Bengaluru",
            "Karnataka", "560058", "Net 30", null, null));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var supplier = (await created.Content.ReadFromJsonAsync<ApiResponse<SupplierDto>>())!.Data!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var locationId = await db.Locations.OrderByDescending(l => l.IsDefault)
            .Select(l => l.LocationId).FirstAsync();
        return (supplier.SupplierId, locationId);
    }

    private static SavePurchaseOrderRequest ReferenceOrder(int supplierId, int locationId,
        byte? discountType = null, decimal? discountValue = null, string? tds = null) =>
        new(DateOnly.FromDateTime(DateTime.UtcNow), supplierId, locationId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)), "Deliver to reception.",
            [
                new(null, "Arabica coffee beans — 1 kg", null, "kg", 20m, 850m, 0m, 5m,
                    "Pantry stock for Services / Coffee", "Pantry & supplies"),
                new(null, "Mesh office chair — Ergo 200", null, "Nos", 6m, 7500m, 0m, 18m,
                    "For the 2nd floor desk zone", "Furniture & fixtures"),
                new(null, "Printer cartridge — HP 682 black", null, "Nos", 4m, 2200m, 0m, 18m,
                    null, "Office supplies")
            ],
            ReferenceNumber: "QT-5521", ShipmentPreference: "Supplier delivery",
            DeliveryAddress: "217, 8th Cross, BHEL Layout, Bengaluru 560098",
            DiscountType: discountType, DiscountValue: discountValue, TdsCode: tds,
            TermsAndConditions: "1. Deliver on time.");

    private static async Task<PurchaseOrderDto> ReadAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<PurchaseOrderDto>>())!.Data!;
    }

    [Fact]
    public async Task Create_RecomputesTheReferenceTotals_WithCgstAndSgstInsideTheState()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin, "29");

        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId)));

        order.Status.Should().Be((byte)PurchaseOrderStatus.Draft);
        order.SubTotal.Should().Be(70_800m);
        order.CgstAmount.Should().Be(5_267m);
        order.SgstAmount.Should().Be(5_267m);
        order.IgstAmount.Should().Be(0m);
        order.TotalAmount.Should().Be(81_334m);
        order.IsInterState.Should().BeFalse();
        order.ReferenceNumber.Should().Be("QT-5521");
        order.Lines.Select(l => l.AccountCategory).Should()
            .Equal("Pantry & supplies", "Furniture & fixtures", "Office supplies");
        order.DeliveryAddress.Should().Be("217, 8th Cross, BHEL Layout, Bengaluru 560098");
        order.History!.Should().ContainSingle(h => h.Action == "Created");
    }

    [Fact]
    public async Task OutOfStateSupplier_IsChargedIgst_AndTdsAndDiscountAreApplied()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin, "27");
        var options = (await admin.GetFromJsonAsync<ApiResponse<PurchaseOrderFormOptionsDto>>(
            "/api/purchase-orders/form-options"))!.Data!;
        var tds = options.TdsOptions.First();

        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId, discountType: 1, discountValue: 5m, tds: tds.Code)));

        order.IsInterState.Should().BeTrue();
        order.DiscountAmount.Should().Be(3_540m);
        order.TaxableAmount.Should().Be(67_260m);
        order.IgstAmount.Should().Be(10_007.30m);
        order.CgstAmount.Should().Be(0m);
        order.TdsAmount.Should().Be(Math.Round(67_260m * tds.Percent / 100m, 2, MidpointRounding.AwayFromZero));
        order.TotalAmount.Should().Be(67_260m + 10_007.30m - order.TdsAmount);
    }

    [Fact]
    public async Task Workflow_SubmitFreezes_ApproveThenSend_ThenReceiveClosesIt_AndEveryStepIsAudited()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin);
        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId)));
        var id = order.PurchaseOrderId;

        // Sending straight from Draft is refused: approval comes first.
        (await admin.PostAsync($"/api/purchase-orders/{id}/send", null)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        order = await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/submit", null));
        order.Status.Should().Be((byte)PurchaseOrderStatus.PendingApproval);

        // Frozen while awaiting approval.
        (await admin.PutAsJsonAsync($"/api/purchase-orders/{id}", ReferenceOrder(supplierId, locationId)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Reject needs a reason, and returns it to Draft.
        (await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/reject",
            new PurchaseOrderDecisionRequest(" "))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        order = await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/reject",
            new PurchaseOrderDecisionRequest("Get a second quote for the chairs")));
        order.Status.Should().Be((byte)PurchaseOrderStatus.Draft);

        await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/submit", null));
        order = await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/approve",
            new PurchaseOrderDecisionRequest("OK within budget")));
        order.Status.Should().Be((byte)PurchaseOrderStatus.Approved);
        order.ApprovedByName.Should().NotBeNullOrWhiteSpace();

        order = await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/send", null));
        order.Status.Should().Be((byte)PurchaseOrderStatus.Sent);

        var partial = await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/receive",
            new ReceiveGoodsRequest(null, "INV-88", null, null,
                [new ReceiveGoodsLineRequest(order.Lines[0].PurchaseOrderLineId, 10m)])));
        partial.Status.Should().Be((byte)PurchaseOrderStatus.PartiallyReceived);

        var full = await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/receive",
            new ReceiveGoodsRequest(null, "INV-89", null, null,
                partial.Lines.Where(l => l.PendingQuantity > 0)
                    .Select(l => new ReceiveGoodsLineRequest(l.PurchaseOrderLineId, l.PendingQuantity))
                    .ToList())));
        full.Status.Should().Be((byte)PurchaseOrderStatus.Received);
        full.History!.Select(h => h.Action).Should().Equal("Created", "Submitted", "Rejected",
            "Submitted", "Approved", "Sent", "Partially received", "Fully received");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var actions = await db.AuditLogs
            .Where(a => a.EntityName == "PurchaseOrder" && a.EntityId == id.ToString())
            .Select(a => a.Action).ToListAsync();
        actions.Should().Contain(["PO_CREATED", "PO_SUBMITTED", "PO_REJECTED", "PO_APPROVED",
            "PO_SENT", "PO_PARTIALLY_RECEIVED", "PO_RECEIVED"]);
    }

    [Fact]
    public async Task Submit_WithoutExpectedDeliveryOrTerms_ReportsBothFields()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin);
        var request = ReferenceOrder(supplierId, locationId) with
        {
            ExpectedDeliveryDate = null,
            DeliveryAddress = null,
            TermsAndConditions = ""
        };
        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders", request));

        var submit = await admin.PostAsync($"/api/purchase-orders/{order.PurchaseOrderId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await submit.Content.ReadAsStringAsync();
        body.Should().Contain("ExpectedDeliveryDate").And.Contain("TermsAndConditions");
    }

    [Fact]
    public async Task Attachments_AreStoredWhileDraft_AndThePdfRenders()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin);
        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId)));
        var id = order.PurchaseOrderId;

        var pdfBytes = "%PDF-1.4\n%quote\n"u8.ToArray();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "supplier_quote.pdf");
        order = await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/attachments", form));
        order.Attachments!.Should().ContainSingle(a => a.FileName == "supplier_quote.pdf");

        using var fake = new MultipartFormDataContent();
        fake.Add(new ByteArrayContent("MZ not a pdf"u8.ToArray()), "file", "evil.pdf");
        (await admin.PostAsync($"/api/purchase-orders/{id}/attachments", fake)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        var download = await admin.GetAsync(
            $"/api/purchase-orders/{id}/attachments/{order.Attachments![0].PurchaseOrderAttachmentId}");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(pdfBytes);

        var pdf = await admin.GetAsync($"/api/purchase-orders/{id}/pdf");
        pdf.EnsureSuccessStatusCode();
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync())[..4].Should().Equal("%PDF"u8.ToArray());

        order = await ReadAsync(await admin.DeleteAsync(
            $"/api/purchase-orders/{id}/attachments/{order.Attachments[0].PurchaseOrderAttachmentId}"));
        order.Attachments!.Should().BeEmpty();
    }

    [Fact]
    public async Task List_SearchesByReferenceAndSupplierName()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin);
        var reference = $"IND-{Guid.NewGuid():N}"[..14];
        await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId) with { ReferenceNumber = reference }));

        var page = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<PurchaseOrderDto>>>(
            $"/api/purchase-orders?search={reference}"))!.Data!;
        page.Items.Should().ContainSingle().Which.ReferenceNumber.Should().Be(reference);
        page.Items[0].CreatedByName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Cancel_IsRefusedOnceGoodsArrive()
    {
        var admin = await AdminAsync();
        var (supplierId, locationId) = await SupplierAndLocationAsync(admin);
        var order = await ReadAsync(await admin.PostAsJsonAsync("/api/purchase-orders",
            ReferenceOrder(supplierId, locationId)));
        var id = order.PurchaseOrderId;
        await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/submit", null));
        await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/approve",
            new PurchaseOrderDecisionRequest(null)));
        order = await ReadAsync(await admin.PostAsync($"/api/purchase-orders/{id}/send", null));
        await ReadAsync(await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/receive",
            new ReceiveGoodsRequest(null, null, null, null,
                [new ReceiveGoodsLineRequest(order.Lines[0].PurchaseOrderLineId, 1m)])));

        (await admin.PostAsJsonAsync($"/api/purchase-orders/{id}/cancel",
            new CancelPurchaseOrderRequest("no longer needed"))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
