	1. Publish an APK

Open a command prompt in your project folder:

cd   "C:\src\HuntCrocMaui\FinTracker\FinTracker"

Then run:

dotnet publish FinTracker.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk

Wait for:

Build succeeded.
2. Find the generated APK

Look under:

C:\src\HuntCrocMaui\FinTracker\FinTracker\bin\Release\net10.0-android\publish\

You should find one or more .apk files.

For installing on another phone, you want the signed APK if the build produced both signed and unsigned variants. It will commonly have a name similar to:

com.companyname.crochuntmpluto-Signed.apk

The exact filename depends on your project configuration.

3. Copy it to another Android phone

You can send/copy the APK using USB, Google Drive, OneDrive, email, WhatsApp, etc.

On the second phone, open the APK. Android may initially say that installation from this source isn't allowed. Choose Settings → Allow from this source, then return and install it.

You do not need Developer Options or USB debugging just to install a properly packaged APK manually.

Authentication
--------------

The application uses one local device account. Registration requires a name, username, and password of at least 8 characters. Passwords are stored only as a PBKDF2-SHA256 hash with a random salt (120,000 iterations); plaintext passwords are never stored. The session is in memory and requires login again after a complete application restart. Logout clears only the session and does not delete financial data.

Financial backups intentionally exclude the local UserAccount table and authentication secrets. Restoring a financial backup preserves the account on the current device. A restored backup therefore does not transfer authentication to another phone.



