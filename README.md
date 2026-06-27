# Load-Stock

Zara, Bershka ve Stradivarius (Türkiye) mağazalarında takip ettiğiniz ürünler **stoğa girince** sizi Windows bildirimiyle uyaran, sistem tepsisinde çalışan küçük bir masaüstü uygulaması.

Bir ürünün linkini yapıştırırsınız, isterseniz belirli beden(ler)i seçersiniz; uygulama arka planda o ürünü düzenli aralıklarla kontrol eder ve tükenmiş bir beden tekrar satışa çıktığında bir bildirim gönderir. Bildirime tıklayınca ürün sayfası tarayıcıda açılır.

## Özellikler

- Zara, Bershka, Stradivarius (Türkiye) ürün linklerini takip etme
- Beden bazında stok takibi (yalnızca seçtiğiniz bedenler ya da hepsi)
- Tükendi → stoğa girdi geçişinde tıklanabilir Windows bildirimi (+ ses)
- Sistem tepsisinde çalışır; pencere kapalıyken de izlemeye devam eder
- Windows açılışında otomatik başlama (isteğe bağlı)
- Uygulama içinde "stoğa girenler" geçmişi
- Tek dosya `.exe` — kurulum gerektirmez

## İndirme ve çalıştırma

1. [Releases](../../releases) sayfasından `LoadStock.exe`'yi indirin.
2. Çift tıklayıp çalıştırın. Uygulama sistem tepsisine yerleşir.

**Gereksinimler**

- Windows 10 (sürüm 2004 / build 19041) veya üzeri
- Microsoft Edge **WebView2** çalışma zamanı — Windows 11 ve güncel Windows 10'da zaten kuruludur. Yoksa uygulama sizi yönlendirir; [buradan](https://developer.microsoft.com/microsoft-edge/webview2/) da kurabilirsiniz.

**SmartScreen uyarısı:** İmzasız bir uygulama olduğu için ilk çalıştırmada Windows "Bilgisayarınız korundu" uyarısı gösterebilir. Devam etmek için **Ek bilgi → Yine de çalıştır**'a tıklayın. Güvenlik için her sürümün SHA-256 özetini Releases sayfasında bulabilir ve indirdiğiniz dosyayla karşılaştırabilirsiniz:

```powershell
Get-FileHash .\LoadStock.exe -Algorithm SHA256
```

## Kullanım

1. **+ Ürün Ekle**'ye tıklayın.
2. Bir Zara / Bershka / Stradivarius ürün linkini yapıştırıp **Getir**'e basın.
3. Tüm bedenleri izleyin ya da yalnızca istediğiniz bedenleri seçin → **Kaydet**.
4. Uygulama bundan sonra o ürünü arka planda kontrol eder. Bir beden stoğa girince bildirim alırsınız.

**Takip** sekmesinde ürünlerin güncel durumunu, **Geçmiş** sekmesinde geçmiş bildirimleri, **Ayarlar** sekmesinde kontrol sıklığı, ses ve otomatik başlatma seçeneklerini görürsünüz.

> İlk eklemede yapılan ilk kontrol yalnızca başlangıç durumunu kaydeder; bu yüzden zaten stokta olan bedenler için bildirim gönderilmez. Bildirim, bir beden tükendikten sonra tekrar stoğa girince gelir.

## Önemli uyarı

Bu araç gayriresmîdir ve Inditex, Zara, Bershka ya da Stradivarius ile hiçbir bağlantısı yoktur. Stok bilgisini mağazaların web sitelerinin kullandığı dahili servislerden okur; bu servisler belgelenmemiştir ve önceden haber vermeden değişebilir. Çok sık sorgulama, sitelerin bot korumasını tetikleyip internet bağlantınızın ilgili siteden geçici olarak engellenmesine yol açabilir. Uygulama bu yüzden kibar aralıklarla sorgular ve yalnızca kişisel, düşük hacimli kullanım için tasarlanmıştır. Aldığınız veriyi yeniden dağıtmayın. Yazılım hiçbir garanti vermez.

## Kaynaktan derleme

Gerekenler: **.NET 8 SDK** ve Windows.

```powershell
# derleme
dotnet build LoadStock.sln -c Release

# testler
dotnet test

# tek dosya, kendine yeten exe
dotnet publish src/LoadStock.App -c Release -r win-x64
```

Üretilen exe: `src/LoadStock.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/LoadStock.exe`

### Proje yapısı

- `LoadStock.Core` — marka istemcileri, URL ayrıştırma, stok karşılaştırma, SQLite deposu (saf, test edilebilir)
- `LoadStock.WebView` — Edge WebView2 üzerinden stok verisi getirme
- `LoadStock.App` — WPF arayüzü, sistem tepsisi, bildirimler, arka plan yoklama
- `LoadStock.Core.Tests` — birim testleri

## Lisans

[MIT](LICENSE)
