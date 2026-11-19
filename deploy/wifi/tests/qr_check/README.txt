Checks the C# QR encoder (backend/.../Services/Wifi/WifiQr.cs, used for the PNG in the Wi-Fi email) against
the qrcode library bundled in frontend/assets/AssetQr-AS1.js, module by module, for all 8 masks.

  dotnet run -c Release -- texts.txt cs.txt qr.png          (prints every matrix to cs.txt, writes a sample PNG)
  node check.mjs texts.txt cs.txt ../../../../frontend/assets/AssetQr-AS1.js

19 Nov 2026: 63 match, 0 differ (versions 1-7, escaped and Unicode text).
