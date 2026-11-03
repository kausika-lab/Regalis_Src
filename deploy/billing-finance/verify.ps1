# Helper for deploy-billing-finance.cmd: the running API publishes the Billing & Finance endpoints.
$ErrorActionPreference = 'Stop'
try {
    $swagger = (Invoke-WebRequest 'http://localhost:81/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 60).Content | ConvertFrom-Json
} catch {
    Write-Host "FAIL- API did not answer: $($_.Exception.Message) - see api\logs"
    exit 1
}
$paths = $swagger.paths.PSObject.Properties.Name
$missing = 0
foreach ($p in '/api/invoices/{id}/payments', '/api/invoices/{id}/payment-context', '/api/payments/register', '/api/payments/{id}/reverse',
               '/api/purchase-bills', '/api/purchase-bills/{id}', '/api/purchase-bills/{id}/approve', '/api/suppliers/{id}/outstanding-bills',
               '/api/vendor-payments', '/api/vendor-payments/{id}/reverse', '/api/parties/{type}/{id}/ledger', '/api/parties/{type}/ageing',
               '/api/bank-import/parse', '/api/bank-import/commit', '/api/bank-import/{batchId}/undo', '/api/bank-lines', '/api/bank-lines/{id}/suggestions',
               '/api/bank-lines/{id}/match', '/api/bank-lines/{id}/unmatch', '/api/tds/summary', '/api/tds/challans', '/api/gst/summary',
               '/api/gst/gstr2b/upload', '/api/gst/returns', '/api/expense-controls', '/api/expense-controls/{expenseId}/approve', '/api/petty-cash',
               '/api/compliance/calendar', '/api/period-locks', '/api/period-locks/{period}/unlock', '/api/export/accounting') {
    if ($paths -contains $p) { Write-Host "OK  - $p" } else { Write-Host "FAIL- missing $p"; $missing++ }
}
exit $missing
