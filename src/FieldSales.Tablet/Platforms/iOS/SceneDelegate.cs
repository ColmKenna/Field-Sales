using Foundation;

namespace FieldSales.Tablet;

// Required by Xcode 27. MAUI forwards scene activation/backgrounding to Window events
// and scene authentication URLs to WebAuthenticator; keep the base implementations.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
