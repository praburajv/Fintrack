using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace MauiApp1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});

#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<ShopRepository>();
            builder.Services.AddSingleton<PaymentModeRepository>();
            builder.Services.AddSingleton<BillTransactionRepository>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<ExpenseListPageModel>();
            builder.Services.AddTransient<ShopListPageModel>();
            builder.Services.AddTransient<ShopEntryPageModel>();
            builder.Services.AddTransient<PaymentModeListPageModel>();
            builder.Services.AddTransient<PaymentModeEntryPageModel>();

            builder.Services.AddTransientWithShellRoute<BillEntryPage, BillEntryPageModel>("bill");
            builder.Services.AddTransientWithShellRoute<ShopListPage, ShopListPageModel>("shops");
            builder.Services.AddTransientWithShellRoute<ShopEntryPage, ShopEntryPageModel>("shop");
            builder.Services.AddTransientWithShellRoute<PaymentModeListPage, PaymentModeListPageModel>("paymentmodes");
            builder.Services.AddTransientWithShellRoute<PaymentModeEntryPage, PaymentModeEntryPageModel>("paymentmode");

            return builder.Build();
        }
    }
}
