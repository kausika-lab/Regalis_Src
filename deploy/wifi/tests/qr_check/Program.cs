using Regalis.Infrastructure.Services.Wifi;
var texts = File.ReadAllLines(args[0]);
var sb = new System.Text.StringBuilder();
foreach (var t in texts) for (int k = -1; k < 8; k++) {
  var m = WifiQr.Encode(t, k); int n = m.GetLength(0);
  sb.Append(k).Append('|');
  for (int y = 0; y < n; y++) { for (int x = 0; x < n; x++) sb.Append(m[y, x] ? '1' : '0'); sb.Append(y < n - 1 ? "/" : ""); }
  sb.Append('\n');
}
File.WriteAllText(args[1], sb.ToString());
File.WriteAllBytes(args[2], WifiQr.Png(texts[0]));
