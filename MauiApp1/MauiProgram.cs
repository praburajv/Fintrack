using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MauiApp1.Services;
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
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<PaymentModeRepository>();
            builder.Services.AddSingleton<BillTransactionRepository>();
            builder.Services.AddSingleton<UserAccountRepository>();
            builder.Services.AddSingleton<IAuthenticationService, LocalAuthenticationService>();
#if ANDROID
            builder.Services.AddSingleton<IBiometricAuthenticationService, Platforms.Android.AndroidBiometricAuthenticationService>();
#else
            builder.Services.AddSingleton<IBiometricAuthenticationService, DefaultBiometricAuthenticationService>();
#endif
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<ICriticalOperationState, CriticalOperationState>();
            builder.Services.AddSingleton<IBackupService, BackupService>();
            builder.Services.AddSingleton<IReadOnlySqlAnalyzerService, ReadOnlySqlAnalyzerService>();
#if ANDROID
            builder.Services.AddSingleton<IBackupFileExporter, Platforms.Android.AndroidBackupFileExporter>();
#else
            builder.Services.AddSingleton<IBackupFileExporter, DefaultBackupFileExporter>();
#endif
#if ANDROID
            builder.Services.AddSingleton<IApplicationCloseService, Platforms.Android.AndroidApplicationCloseService>();
#else
            builder.Services.AddSingleton<IApplicationCloseService, DefaultApplicationCloseService>();
#endif
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddTransient<ShopBillDetailsPageModel>();
            builder.Services.AddTransient<MonthlyExpenseCalendarPageModel>();
            builder.Services.AddTransient<DailyBillListPageModel>();
            builder.Services.AddTransient<DailyDashPageModel>();
            builder.Services.AddTransient<PaymentModeBillListPageModel>();
            builder.Services.AddSingleton<ExpenseListPageModel>();
            builder.Services.AddTransient<ShopListPageModel>();
            builder.Services.AddTransient<ShopEntryPageModel>();
            builder.Services.AddTransient<CategoryListPageModel>();
            builder.Services.AddTransient<CategoryEntryPageModel>();
            builder.Services.AddTransient<SetupDataPageModel>();
            builder.Services.AddTransient<PaymentModeListPageModel>();
            builder.Services.AddTransient<PaymentModeEntryPageModel>();
            builder.Services.AddTransient<BackupRestorePageModel>();
            builder.Services.AddTransient<LoginPageModel>();
            builder.Services.AddTransient<RegisterPageModel>();
            builder.Services.AddTransient<PasswordResetPageModel>();
            builder.Services.AddTransient<DatabaseToolsPageModel>();
            builder.Services.AddTransient<SetupPageModel>();
            builder.Services.AddTransient<ReadOnlySqlAnalyzerPageModel>();

            builder.Services.AddTransientWithShellRoute<BillEntryPage, BillEntryPageModel>("bill");
            builder.Services.AddTransientWithShellRoute<ShopListPage, ShopListPageModel>("shops");
            builder.Services.AddTransientWithShellRoute<ShopEntryPage, ShopEntryPageModel>("shop");
            builder.Services.AddTransientWithShellRoute<CategoryListPage, CategoryListPageModel>("categories");
            builder.Services.AddTransientWithShellRoute<CategoryEntryPage, CategoryEntryPageModel>("category");
            builder.Services.AddTransientWithShellRoute<SetupDataPage, SetupDataPageModel>("setupdata");
            builder.Services.AddTransientWithShellRoute<PaymentModeListPage, PaymentModeListPageModel>("paymentmodes");
            builder.Services.AddTransientWithShellRoute<PaymentModeEntryPage, PaymentModeEntryPageModel>("paymentmode");
            builder.Services.AddTransientWithShellRoute<ShopBillDetailsPage, ShopBillDetailsPageModel>("shopbills");
            builder.Services.AddTransientWithShellRoute<MonthlyExpenseCalendarPage, MonthlyExpenseCalendarPageModel>("monthlycalendar");
            builder.Services.AddTransientWithShellRoute<DailyBillListPage, DailyBillListPageModel>("dailybills");
            builder.Services.AddTransientWithShellRoute<DailyDashPage, DailyDashPageModel>("dailydash");
            builder.Services.AddTransientWithShellRoute<PaymentModeBillListPage, PaymentModeBillListPageModel>("paymentmodebills");
            builder.Services.AddTransientWithShellRoute<BackupDataPage, BackupDataPageModel>("backup");
            builder.Services.AddTransientWithShellRoute<RestoreDataPage, RestoreDataPageModel>("restore");
            builder.Services.AddTransientWithShellRoute<LoginPage, LoginPageModel>("login");
            builder.Services.AddTransientWithShellRoute<RegisterPage, RegisterPageModel>("register");
            builder.Services.AddTransientWithShellRoute<PasswordResetPage, PasswordResetPageModel>("passwordreset");
            builder.Services.AddTransientWithShellRoute<DatabaseToolsPage, DatabaseToolsPageModel>("databasetools");
            builder.Services.AddTransientWithShellRoute<BackupRestorePage, BackupRestorePageModel>("backuprestore");
            builder.Services.AddTransientWithShellRoute<ReadOnlySqlAnalyzerPage, ReadOnlySqlAnalyzerPageModel>("sqlqa");

            return builder.Build();
        }
    }
}
