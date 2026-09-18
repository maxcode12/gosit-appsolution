// Stripe's Payment Element, mounted into a BlazorWebView page.
//
// A BlazorWebView is a real browser surface (WebView2 on Windows, WKWebView on iOS,
// a Chromium view on Android), so the same Stripe.js used on the web app runs here
// unmodified — card data goes straight from Stripe's own iframe to Stripe, never through
// this script or C#.
//
// `redirect: 'if_required'` is the one difference from the web integration: a browser tab
// can be sent away and back for a redirect-based payment method, but there is nowhere for
// a WebView to "come back" to. Every payment method offered here must be able to resolve
// without leaving the page — which card payments do — and Stripe is told not to redirect
// unless a method genuinely requires it.
window.gositStripe = (function () {
    let stripe = null;
    let elements = null;

    function mount(elementId, publishableKey, clientSecret) {
        stripe = Stripe(publishableKey);
        elements = stripe.elements({ clientSecret, appearance: { theme: "stripe" } });
        elements.create("payment").mount("#" + elementId);
    }

    async function confirm() {
        if (!stripe || !elements) {
            return "Payment form is not ready yet.";
        }

        const { error } = await stripe.confirmPayment({
            elements,
            redirect: "if_required",
        });

        return error ? (error.message || "The payment could not be completed.") : null;
    }

    return { mount, confirm };
})();
