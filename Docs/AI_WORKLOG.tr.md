# AI Çalışma Günlüğü

[English](AI_WORKLOG.md) | **Türkçe**

Proje baştan sona Claude Code ile, Unity editörüne **MCP for Unity** (CoplayDev) üzerinden bağlanarak geliştirildi.
Asset analizi ve optimizasyonu için Blender arayüzsüz (headless) script'lerle AI tarafından sürüldü.

**Çalışma şekli** (`CLAUDE.md`'de yazılı kurallar):
- Her seferinde tek sistem: AI sistemi yazar ve açıklar, onay gelmeden bir sonrakine geçmez.
- Her Core sistemi EditMode testleriyle gelir; her sistem `Docs/TECH.tr.md`'de anlatılır.
- Kararlar (kamera açısı, yatay yön, Mixamo animasyonları, kendi joystick'imiz, JSON kayıt, Git LFS kullanmamak,
  performans hedefi) proje başında `CLAUDE.md`'ye yazıldı; AI her oturumda bunlara göre çalıştı.
- Editörde yapılan her iş MCP ile **ölçülerek ya da render alınarak doğrulandı**; "çalışıyor olmalı" kabul edilmedi.
  Telefondaki sonuçlar ise her zaman gerçek cihazda ölçüldü.

## Kritik karar 1: Oyun mantığı Unity'den bağımsız, saf C# ve testli

**Seçenekler.** (a) Klasik Unity yaklaşımı: her düşmanda bir MonoBehaviour, Update, collider'lar ve fizik.
(b) Oyun mantığı saf C# sınıflarında, MonoBehaviour'lar sadece çizim ve girdi için ince bir katman.
(c) (b) + VContainer gibi bir DI container.

**Karar: (b).** Container değerlendirildi ve reddedildi: bu ölçekte elle bağlama daha kısa, paket bağımlılığı
getirmiyor ve her bağlantı tek dosyada görünür.

**Neden.**
- 150 düşmanın her biri için bir `Update()` yerine tek döngü; mobil CPU için baştan doğru yapı.
- Fizik motoru yok: mermi isabeti, merminin o frame'de kat ettiği yol parçasıyla test ediliyor; sonuçlar
  deterministik ve testte doğrulanabilir.
- Bütün oyun Play modu olmadan simüle edilebiliyor: 3 dakikalık bir tur testte milisaniyeler içinde oynatılıyor.

**Sonucu.** 278 EditMode testi. Testler gerçek hatalar yakaladı: benchmark'ın yüzdelik hesabında float yuvarlama
hatası (`0.99f * 100` 99'dan biraz büyük çıkıyordu), kamera sarsıntısının akıcı değil titrek olduğu (frame başına
ofsetin %63'ü kadar sıçrama). Bu karar son eklenen Endless modunda da işe yaradı: Unity MCP'ye erişilemeyen bir bulut
oturumunda Core sistemleri ve testleri Unity olmadan derlenip çalıştırıldı, zorluk ayarı da Core'u bir bot ile
simüle ederek yapıldı.

## Kritik karar 2: Önce ölç, sonra düşmanın asset'ini yeniden üret

**Durum.** Referans build telefonda 14,6 FPS verdi. İlk refleks kod optimizasyonu olabilirdi; benchmark ise frame
süresinin GPU süresine eşit (~69 ms), CPU'nun ise sadece ~17 ms olduğunu gösterdi. Oyun **GPU'ya takılıydı**; CPU'yu
hızlandırmak hiçbir şey kazandırmazdı.

**Karar.** Önce aynı koşulları garanti eden bir oyun içi benchmark modu yazıldı (sabit seed, hasar almayan ve
hareketsiz oyuncu, 150 düşman, JSON + logcat çıktısı). Sonra en büyük GPU maliyeti hedeflendi: 150 x 36.902 üçgenlik,
65 kemikli, 8 adet 4096² dokulu düşman. AI Blender'da önce modeli analiz eden, sonra optimize sürümü üreten script'ler
yazdı: iki LOD (4.500 / 1.500 üçgen), parmak kemikleri kaldırıldı (65 -> 22, ağırlıkları ele aktarılarak), vertex başına
4 kemik etkisi, iki materyal tek atlasa. Orijinal dosyalar hiç değiştirilmedi; referans build dürüst bir karşılaştırma
olarak kaldı.

**Sonucu.** Tek adımda 14,6 -> 49,4 FPS. Toplam yedi ölçülmüş adımdan sonra 83,8 FPS (x5,7). Her adım aynı cihazda,
aynı benchmark ile, bir sonrakine geçmeden ölçüldü (`Docs/PERFORMANCE.tr.md`). Reddedilen bir alternatif: sadece uzak
LOD'da gölgeyi kapatmak. Denendi; gölgeler ekranın üst yarısında aniden kayboluyordu, yerine bütün düşmanlara blob
shadow verildi.

**Yakalanan hatalar.** Blender'da materyal listesini temizlemek yüzlerin materyal indekslerini de sıfırladı ve mesh tek
submesh'e düştü (Unity'de submesh sayısı okunarak fark edildi). Ölçeklenmeyen bir doku, Blender pikselleri tembel
yüklediği için boş kaydediliyordu. İkisi de çıktı Unity'de kontrol edilerek yakalandı.

## Kritik karar 3: Düşman kalabalığı için kuvvet değil konum düzeltmesi

**Sorun.** Düşmanlar oyuncunun üstünde tek bir yığına dönüşüyordu; tek tek seçilemiyor ve hepsi aynı anda
vurabiliyordu.

**İlk deneme (reddedildi).** Birbirine çok yaklaşan düşmanları bir kuvvetle itmek. MCP ile Play modunda ölçüldüğünde
arka sıralar ön sıraları sıkıştırdı: kalabalık ~4 m'lik bir diske döndü, en yakın çift 0,32 m, oyuncunun 1,5 m
yakınında 36 düşman vardı.

**Karar.** İç içe geçen her çift, örtüşmenin yarısı kadar birbirinden uzaklaştırılıyor (konum düzeltmesi); komşular
bir spatial grid'den geliyor (150 düşmanda 22.350 yerine ~1.600 mesafe kontrolü). Yarıçap ve sertlik, 150 düşmanlık
simülasyonda ortalama komşu mesafesi, kalabalık yarıçapı, aynı anda saldıran düşman sayısı ve tick süresi MCP ile
ölçülerek seçildi (1,2 m seçildi, ~6 düşman aynı anda saldırıyor).

**Sonradan çıkan sorun.** Telefonda kalabalığın titrediği görüldü. Ölçüldüğünde her düşman frame başına ~18 cm
ileri-geri gidiyordu: 30 FPS'lik uzun bir frame'de yürüme adımı, ayrışmanın bir adımda düzelttiğinden büyüktü.
Simülasyon 1/60 sn'lik alt adımlara bölündü ve sertlik 0,5'e indirildi; titreme gitti.

## Unity MCP ile uçtan uca görev: düşman animasyonu optimizasyonu

Case'in istediği "editör durumunu oku -> işlem yap -> doğrula" akışının en net örneği:

1. **Oku.** Benchmark sahnesinde (150 düşman) MCP ile Animator güncelleme süresi, Transform sayısı ve frame süresi
   ölçüldü: Animator 2,33 ms, 3.978 Transform, frame 3,76 ms.
2. **İşlem.** Düşman modelinde **Optimize Game Objects** açıldı (kemikler GameObject olmaktan çıkar). Humanoid
   retargeting'i her frame yapmak yerine editörde bir kez yapan bir araç yazıldı (`GenericClipBaker`): Humanoid klip
   30 FPS ile örneklenip her kemiğin yerel dönüşü Generic bir klibe kaydediliyor. Animator controller bu kliplerle
   yeniden kuruldu, ekran dışı düşmanlar için **Cull Completely** açıldı.
3. **Doğrula.** Aynı ölçüm tekrarlandı: 459 Transform, Animator 1,07 ms (-%54), frame 3,28 ms. Pozların Humanoid
   oynatmayla aynı olduğu, iki farklı anda yan yana render alınarak kontrol edildi. Telefonda benchmark: CPU ana
   thread 9,9 -> 8,4 ms, bellek 4-5 MB daha az.

MCP ile yapılan diğer işler: sahnenin, UI'ın ve Animator controller'ların kurulması, kamera kadrajının render alınarak
seçilmesi, rifle'ın el kemiğindeki konumunun animasyon pozundan hesaplanması, materyal varyantlarının yan yana render
ile karşılaştırılması, oyun hissi ayarlarının (kamera sarsıntısı, isabet kıvılcımı) frame frame ölçülerek yapılması.

## AI'ın yanıldığı ve düzeltildiği yerler

- **Can barı hep doluydu.** AI dolgu görselinin köşeli görünmesini düzeltmek için sprite'ı kaldırmıştı; uGUI sprite'ı
  olmayan bir Image'da `fillAmount`'u sessizce yok sayıyor. MCP ile yapılan kontrol sadece `fillAmount` değerini
  okuduğu için hatayı görmedi; **telefonda oynarken fark edildi**. Düzeltme, dolgunun gerçek genişliği ölçülerek ve
  HUD render alınarak doğrulandı. Ders: bir değeri okumak, sonucu görmek değildir.
- **Hiçbir şey ölçmeyen test.** İlk "sadece komşular kontrol ediliyor" testi düşmanları tam 2 m arayla diziyordu;
  hiçbiri komşu hücreye düşmediği için kontrol sayısı 0 çıkıyor ve test boşuna geçiyordu. Rastgele bir kalabalık ve
  "en az bir kontrol yapıldı" şartıyla düzeltildi.
- **Görünmez şeffaf materyal.** URP materyalini özellikleri tek tek atayarak şeffaf yapmak yetmedi; quad çizilmedi.
  Önce opak kırmızı bir materyalle render edilip sorun materyale indirgendi, sonra URP'nin kendi
  `BaseShaderGUI.SetupMaterialBlendMode` fonksiyonuyla kuruldu.
- **Zayıf kamera sarsıntısı.** İlk değerlerle vuruş telefonda görünmüyordu (2 cm). Kamera sapması frame frame
  ölçülüp üç turda ayarlandı; ayrıca yatma (roll) eklendi.
- **Yanlış adlandırılmış ölçüm.** Bir benchmark JSON dosyası bir önceki adımın sonucuyla karıştırılmıştı; dosyanın
  içindeki sürüm ve tarih alanlarından fark edilip silindi.

## Son ek: Endless modu

Case teslim edilebilir hâldeyken ayrı bir Endless modu eklendi (XP ve can düşürme, seviye barı, 3 karttan birini
seçme, zamanla güçlenen düşmanlar). Bu iş bulutta çalışan bir Claude Code oturumunda yapıldı; oradan
geliştiricinin bilgisayarındaki Unity MCP sunucusuna erişilemiyordu. Bu yüzden:
- Core sistemleri ve testleri, UnityEngine matematik tiplerinin küçük bir taslağıyla .NET'te derlenip çalıştırıldı
  (278 testin hepsi geçti); Unity ve Editor kodu gerçek Unity referans DLL'lerine karşı derlenerek kontrol edildi.
- Zorluk ayarı, arenada kaçan ve rastgele kart seçen bir botla Core simüle edilerek yapıldı.
- Sahneyi elle ya da YAML düzenleyerek kurmak yerine, işi tek tıkla yapan bir editör komutu yazıldı
  (**Tools > Arena Survivor > Setup Endless Mode**); yeni UI mevcut, stillenmiş objeler kopyalanarak kuruluyor.
- Bir hata gözden kaçtı: satır sonu temizlenirken editör assembly tanımının kapanış parantezi de silindi. .NET
  kontrolleri `.asmdef` dosyalarını okumadığı için hatayı merge sonrasında Unity bildirdi; tek satırlık bir commit
  ile düzeltildi.
