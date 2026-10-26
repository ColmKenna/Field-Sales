using Android.App;
using Android.Content;
using Android.Content.PM;
using FieldSales.Snapshot.Contracts;

namespace FieldSales.Tablet;

/// <summary>Receives the identity host's redirect to <see cref="TabletClientContract.RedirectUri"/> from the browser.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "com.fieldsales.tablet", DataHost = "signin-callback")]
public class SignInCallbackActivity : WebAuthenticatorCallbackActivity
{
}
