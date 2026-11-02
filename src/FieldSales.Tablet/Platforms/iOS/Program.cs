using UIKit;

namespace FieldSales.Tablet;

public static class Program
{
    public static void Main(string[] args)
    {
#if DEBUG
        Console.WriteLine("FieldSalesTiming: ios managed-main");
#endif
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
