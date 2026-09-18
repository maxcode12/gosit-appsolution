<#
.SYNOPSIS
    Verifies subscription billing end to end against a real Stripe test account.

.DESCRIPTION
    Everything the automated suite cannot prove without credentials:

      1. The API reports billing as enabled and the plans are linked to real Prices.
      2. Subscribing returns a genuine checkout.stripe.com URL — and grants nothing.
      3. Completing that checkout with a test card activates the plan, via the webhook.
      4. Cancelling stops the renewal at Stripe and keeps the paid period.

    Step 3 needs a human to complete a hosted checkout page in a browser, so the script
    pauses, hands you the URL and a test card number, then polls until the webhook lands.

.PREREQUISITES
    - scripts/setup-stripe-test.ps1 has been run
    - Stripe:WebhookSecret is set in user secrets
    - `stripe listen --forward-to http://localhost:5199/api/v1/webhooks/stripe` running
    - the API running on http://localhost:5199
#>

[CmdletBinding()]
param(
    [string]$ApiBase = "http://localhost:5199",
    [int]$WebhookTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Step([string]$text) { Write-Host "`n$text" -ForegroundColor Cyan }
function Pass([string]$text) { Write-Host "  PASS  $text" -ForegroundColor Green }
function Fail([string]$text) { Write-Host "  FAIL  $text" -ForegroundColor Red; exit 1 }

# ---- a throwaway account -----------------------------------------------------
$email = "stripe-verify-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())@example.test"
$password = "correct-horse-battery-staple"

Step "Registering $email"

$registered = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/auth/register" `
    -ContentType "application/json" `
    -Body (@{
        email = $email; password = $password
        firstName = "Stripe"; lastName = "Verify"; role = "CareRequester"
    } | ConvertTo-Json)

$auth = @{ Authorization = "Bearer $($registered.accessToken)" }
Pass "registered"

# ---- 1. billing is configured -----------------------------------------------
Step "1. Checking billing is configured"

$mine = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth

if (-not $mine.billingEnabled) {
    Fail "the API reports billingEnabled=false. Is Stripe:SecretKey set and the API restarted?"
}

Pass "billingEnabled=true, currently on $($mine.planCode)"

# ---- 2. subscribing returns checkout and grants nothing ----------------------
Step "2. Subscribing to Go Sit Plus"

$result = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth `
    -ContentType "application/json" `
    -Body (@{ planCode = "PLUS_REQUESTER"; promoCode = $null } | ConvertTo-Json)

if (-not $result.checkoutUrl) {
    Fail "no checkoutUrl returned. Is Stripe:Prices:PLUS_REQUESTER set and the API restarted?"
}

if ($result.checkoutUrl -notlike "https://checkout.stripe.com/*") {
    Fail "checkoutUrl is not a Stripe URL: $($result.checkoutUrl)"
}

Pass "got a real Stripe checkout URL"

if ($result.subscription) { Fail "a subscription was returned before payment" }
Pass "no subscription granted before payment"

$before = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth

if ($before.planCode -ne "FREE_REQUESTER") {
    Fail "the account left the free plan before paying: $($before.planCode)"
}

Pass "account is still on the free plan"

# ---- 3. complete checkout ----------------------------------------------------
Step "3. Complete the checkout in your browser"

Write-Host "  $($result.checkoutUrl)"
Write-Host "  Test card 4242 4242 4242 4242, any future expiry, any CVC, any postcode."
Write-Host "  Waiting for the webhook (up to $WebhookTimeoutSeconds s)..."

$deadline = (Get-Date).AddSeconds($WebhookTimeoutSeconds)
$active = $false

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3

    $now = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth

    if ($now.planCode -eq "PLUS_REQUESTER") {
        $active = $true
        break
    }
}

if (-not $active) {
    Fail "the plan never activated. Is `stripe listen` running and Stripe:WebhookSecret set?"
}

Pass "the webhook activated the plan"

$now = Invoke-RestMethod -Method Get -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth

if ($now.status -ne "Active") { Fail "expected status Active, got $($now.status)" }
if (-not $now.currentPeriodEndUtc) { Fail "no billing period was recorded" }
if (-not $now.hasBillingPortal) { Fail "no provider customer was linked" }

Pass "status=Active, renews $($now.currentPeriodEndUtc)"
Pass "features granted: $($now.features -join ', ')"

# ---- 4. billing portal -------------------------------------------------------
Step "4. Opening the billing portal"

$portal = Invoke-RestMethod -Method Post -Uri "$ApiBase/api/v1/me/billing-portal" -Headers $auth

if ($portal.url -notlike "https://billing.stripe.com/*") {
    Fail "not a Stripe billing portal URL: $($portal.url)"
}

Pass "portal URL issued"

# ---- 5. cancelling stops the renewal ----------------------------------------
Step "5. Cancelling"

$cancelled = Invoke-RestMethod -Method Delete -Uri "$ApiBase/api/v1/me/subscription" -Headers $auth

if (-not $cancelled.cancelAtPeriodEnd) { Fail "cancelAtPeriodEnd was not set" }
Pass "renewal stopped"

# Someone who has paid for a month gets the month.
if ($cancelled.features.Count -eq 0) { Fail "features were confiscated on cancellation" }
Pass "features kept until the period ends"

Write-Host "`nAll checks passed." -ForegroundColor Green
Write-Host "Check the Stripe dashboard (test mode) for the customer, subscription and invoice."
Write-Host "`nClean up the throwaway account when you are done:"
Write-Host "  $email"
