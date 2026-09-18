<#
.SYNOPSIS
    Creates the Stripe test-mode Products and Prices for Go Sit's plans, and records the
    Price ids in user secrets.

.DESCRIPTION
    Run this once, against a Stripe account in TEST MODE, after setting your secret key:

        dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/backend/GoSit.Api

    It is idempotent: each Price is looked up by `lookup_key` first, so re-running finds
    what already exists rather than creating duplicates.

    It refuses to run with a live key. Creating pricing in a live account is not
    something a setup script should be able to do by accident.

.NOTES
    This writes to your Stripe account. Read it before running it.
#>

[CmdletBinding()]
param(
    [string]$Project = "src/backend/GoSit.Api"
)

$ErrorActionPreference = "Stop"

function Get-Secret([string]$key) {
    $line = (dotnet user-secrets list --project $Project 2>$null) |
        Where-Object { $_ -like "$key = *" } |
        Select-Object -First 1

    if (-not $line) { return $null }

    return $line.Substring($line.IndexOf("=") + 1).Trim()
}

$secretKey = Get-Secret "Stripe:SecretKey"

if (-not $secretKey) {
    Write-Host "No Stripe:SecretKey in user secrets. Set it first:" -ForegroundColor Yellow
    Write-Host "  dotnet user-secrets set `"Stripe:SecretKey`" `"sk_test_...`" --project $Project"
    exit 1
}

if (-not $secretKey.StartsWith("sk_test_")) {
    Write-Host "That key is not a test-mode key (expected sk_test_...). Refusing." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $secretKey" }

# Prices are matched on lookup_key so this script can find its own previous work.
# Amounts are in cents and must match SubscriptionPlanSeed, which is what the UI shows.
$plans = @(
    @{ Code = "PLUS_REQUESTER"; Name = "Go Sit Plus"; Amount = 1200; LookupKey = "gosit_plus_requester_monthly" }
    @{ Code = "PRO_CAREGIVER";  Name = "Go Sit Pro";  Amount = 1900; LookupKey = "gosit_pro_caregiver_monthly" }
)

foreach ($plan in $plans) {
    Write-Host "`n$($plan.Name) ($($plan.Code))" -ForegroundColor Cyan

    $existing = Invoke-RestMethod -Method Get -Headers $headers `
        -Uri "https://api.stripe.com/v1/prices?lookup_keys[]=$($plan.LookupKey)&limit=1"

    if ($existing.data.Count -gt 0) {
        $priceId = $existing.data[0].id
        Write-Host "  Price already exists: $priceId"
    }
    else {
        $product = Invoke-RestMethod -Method Post -Headers $headers `
            -Uri "https://api.stripe.com/v1/products" `
            -Body @{ name = $plan.Name }

        Write-Host "  Created product $($product.id)"

        $body = @{
            product                 = $product.id
            currency                = "cad"
            unit_amount             = $plan.Amount
            lookup_key              = $plan.LookupKey
            "recurring[interval]"   = "month"
        }

        $price = Invoke-RestMethod -Method Post -Headers $headers `
            -Uri "https://api.stripe.com/v1/prices" -Body $body

        $priceId = $price.id
        Write-Host "  Created price $priceId"
    }

    dotnet user-secrets set "Stripe:Prices:$($plan.Code)" $priceId --project $Project | Out-Null
    Write-Host "  Recorded Stripe:Prices:$($plan.Code)"
}

Write-Host "`nNext, in a second terminal, forward webhooks:" -ForegroundColor Cyan
Write-Host "  stripe listen --forward-to http://localhost:5199/api/v1/webhooks/stripe"
Write-Host "Then put the whsec_... it prints into user secrets:"
Write-Host "  dotnet user-secrets set `"Stripe:WebhookSecret`" `"whsec_...`" --project $Project"
Write-Host "`nThen run scripts/verify-stripe-e2e.ps1"
