1. Publish an APK

Open a command prompt in your project folder:

cd /d "C:\src\HuntCrocMaui\MauiApp1\MauiApp1"

Then run:

dotnet publish CrocHuntMPluto.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk

Wait for:

Build succeeded.
2. Find the generated APK

Look under:

C:\src\HuntCrocMaui\MauiApp1\MauiApp1\bin\Release\net10.0-android\publish\

You should find one or more .apk files.

For installing on another phone, you want the signed APK if the build produced both signed and unsigned variants. It will commonly have a name similar to:

com.companyname.crochuntmpluto-Signed.apk

The exact filename depends on your project configuration.

3. Copy it to another Android phone

You can send/copy the APK using USB, Google Drive, OneDrive, email, WhatsApp, etc.

On the second phone, open the APK. Android may initially say that installation from this source isn't allowed. Choose Settings → Allow from this source, then return and install it.

You do not need Developer Options or USB debugging just to install a properly packaged APK manually.




