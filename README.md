# Wimmia

> A 2D Unity game prototype focused on responsive movement, combat systems, level mechanics, and boss encounters.  
> Akıcı hareket, savaş sistemleri, bölüm mekanikleri ve boss karşılaşmalarına odaklanan 2D Unity oyun prototipi.

[English](#english) · [Türkçe](#türkçe)

---

## English

### Overview

Wimmia is a 2D game development project built with Unity and C#. It is used to design, implement, and test gameplay systems such as player movement, combat, environmental interaction, camera behavior, enemies, and boss encounters.

The main goal of the project is not only to create playable content, but also to build gameplay systems that are understandable, reusable, and easier to improve over time.

### Core Areas

- Responsive 2D player movement
- Combat and damage systems
- Enemy behavior and hit reactions
- Room-based level structure
- Camera boundaries and room transitions
- Environmental mechanics
- Boss attacks and encounter design
- Gameplay feedback such as hit stop, knockback, particles, and camera shake

### Current Boss Prototype

The project currently includes work on a large boss encounter designed around readable attack patterns and active player movement.

The boss system explores:

- Multiple attack patterns
- Projectile and area-based attacks
- Movement-restricting hazards
- Damageable weak points
- Attack timing and recovery windows
- Room-specific camera framing
- Visual and gameplay feedback during combat

### Technologies

- Unity 6
- C#
- Unity 2D Physics
- Cinemachine
- Tilemap
- Unity Input System
- Git
- GitHub

### Unity Version

The project currently uses:

```text
Unity 6000.3.9f1
```

Using the same Unity version is recommended to avoid unnecessary project upgrades and package conflicts.

### Running the Project

1. Clone the repository:

```bash
git clone https://github.com/muhammedcanarica/Wimmia.git
```

2. Open Unity Hub.
3. Select **Add project from disk**.
4. Choose the cloned project folder.
5. Open the project with Unity `6000.3.9f1`.
6. Open the main gameplay scene and press Play.

### Project Structure

The project is organized around separate gameplay responsibilities. Movement, combat, camera behavior, enemies, environmental mechanics, and boss logic are developed as independent systems where possible.

This approach makes individual mechanics easier to test and prevents one large controller script from becoming responsible for the entire game, a fate suffered by far too many Unity projects.

### Project Status

Wimmia is an active gameplay prototype. Some systems are complete enough to test, while others are still being tuned or expanded.

### Planned Improvements

- More polished boss attack transitions
- Improved attack telegraphing
- Better difficulty pacing
- Additional sound and visual feedback
- Cleaner separation between boss phases
- Playtesting and balance adjustments
- Gameplay video and downloadable build

### Media

Add gameplay screenshots or a GIF here:

```md
![Wimmia gameplay](docs/wimmia-gameplay.gif)
```

---

## Türkçe

### Genel Bakış

Wimmia, Unity ve C# ile geliştirilen bir 2D oyun projesidir. Oyuncu hareketi, savaş sistemi, çevre etkileşimleri, kamera davranışı, düşmanlar ve boss karşılaşmaları gibi gameplay sistemlerini tasarlamak, geliştirmek ve test etmek için kullanılmaktadır.

Projenin amacı yalnızca oynanabilir içerik üretmek değil; aynı zamanda anlaşılabilir, tekrar kullanılabilir ve zaman içinde daha kolay geliştirilebilir gameplay sistemleri oluşturmaktır.

### Temel Çalışma Alanları

- Akıcı 2D oyuncu hareketi
- Savaş ve hasar sistemleri
- Düşman davranışları ve hasar tepkileri
- Oda tabanlı bölüm yapısı
- Kamera sınırları ve oda geçişleri
- Çevresel mekanikler
- Boss saldırıları ve karşılaşma tasarımı
- Hit stop, knockback, parçacık ve kamera sarsıntısı gibi gameplay geri bildirimleri

### Mevcut Boss Prototipi

Projede şu anda okunabilir saldırı düzenleri ve aktif oyuncu hareketi üzerine kurulu büyük bir boss karşılaşması geliştirilmektedir.

Boss sistemi şu konular üzerinde çalışır:

- Birden fazla saldırı düzeni
- Mermi ve alan tabanlı saldırılar
- Oyuncunun hareketini kısıtlayan tehlikeli alanlar
- Hasar alabilen zayıf noktalar
- Saldırı zamanlaması ve toparlanma aralıkları
- Odaya özel kamera kadrajı
- Savaş sırasında görsel ve mekanik geri bildirim

### Kullanılan Teknolojiler

- Unity 6
- C#
- Unity 2D Physics
- Cinemachine
- Tilemap
- Unity Input System
- Git
- GitHub

### Unity Sürümü

Proje şu Unity sürümünü kullanmaktadır:

```text
Unity 6000.3.9f1
```

Gereksiz proje yükseltmelerini ve paket uyumsuzluklarını önlemek için aynı Unity sürümünün kullanılması önerilir.

### Projeyi Çalıştırma

1. Repoyu klonla:

```bash
git clone https://github.com/muhammedcanarica/Wimmia.git
```

2. Unity Hub'ı aç.
3. **Add project from disk** seçeneğine bas.
4. Klonlanan proje klasörünü seç.
5. Projeyi Unity `6000.3.9f1` ile aç.
6. Ana gameplay sahnesini açıp Play tuşuna bas.

### Proje Yapısı

Projede gameplay sorumlulukları mümkün olduğunca ayrı sistemler hâlinde tutulur. Hareket, savaş, kamera, düşmanlar, çevre mekanikleri ve boss mantığı bağımsız olarak geliştirilmeye çalışılır.

Bu yaklaşım, mekaniklerin ayrı ayrı test edilmesini kolaylaştırır ve tek bir controller scriptinin bütün oyundan sorumlu olduğu o meşhur Unity felaketini önler.

### Proje Durumu

Wimmia aktif olarak geliştirilen bir gameplay prototipidir. Bazı sistemler test edilebilir durumda tamamlanmışken bazıları hâlâ ayarlanmakta ve genişletilmektedir.

### Planlanan Geliştirmeler

- Boss saldırıları arasında daha iyi geçişler
- Saldırıların daha okunabilir şekilde önceden gösterilmesi
- Daha dengeli zorluk ilerleyişi
- Ses ve görsel geri bildirimlerin geliştirilmesi
- Boss aşamalarının daha temiz ayrılması
- Oynanış testleri ve denge ayarları
- Gameplay videosu ve indirilebilir build

### Medya

Buraya gameplay ekran görüntüsü veya GIF eklenebilir:

```md
![Wimmia gameplay](docs/wimmia-gameplay.gif)
```
