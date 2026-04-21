# Admin Panel API Güncelleme Dokümantasyonu

Bu doküman, frontend tarafında geliştirilen admin paneline ait özelliklerin (Tesis ve Orduevi güncellemeleri) aksaksız çalışabilmesi için .NET backend tarafında yapılması gereken **RESTful endpoint** güncellemelerini, **C# DTO** yapılarını ve **JSON parametrelerini** detaylandırmaktadır.

---

## 1. Tesis (Facility) Güncelleme Ucu (Endpoint)

Frontend üzerinde tesis düzenleme ekranında; tesisin temel bilgilerinin yanı sıra **çalışan personel listesi**, **kapalı olduğu günler**, **alt hizmetleri (fiyatlandırma dahil)** ve **tesis görseli (base64)** tek bir form üzerinde yönetilmektedir.

### **Endpoint Detayları**
- **HTTP Metodu:** `PUT`
- **Route (Yol):** `/api/Reservations/facilities/{id}`
- **Açıklama:** Var olan bir tesisin tüm bilgilerini (personel, hizmetler, kapalı günler vb. dâhil) günceller.

### **C# DTO Yapıları**

Backend tarafında oluşturulması veya güncellenmesi gereken model sınıfları aşağıdaki gibidir:

```csharp
public class UpdateFacilityRequestDto
{
    public string Name { get; set; }
    public string Category { get; set; }
    
    // 'WalkIn' veya 'Appointment'
    public string AppointmentMode { get; set; } 
    
    public int? MaxConcurrency { get; set; }
    public int? BufferMinutes { get; set; }
    public int? DefaultSlotDurationMinutes { get; set; }
    
    // "09:00", "18:00" formatlarında
    public string OpeningTime { get; set; } 
    public string ClosingTime { get; set; } 
    
    public string Description { get; set; }

    // --- FRONTEND'İN İHTİYAÇ DUYDUĞU YENİ ALANLAR ---

    // Fotoğraf yükleme base64 string olarak iletiliyor (örn: data:image/png;base64,iVBO...)
    public string Image { get; set; } 

    // Tesisin personelleri (sadece isim listesi veya ileride detaylı obje olabilir, şu an string dizisi)
    public List<string> Staff { get; set; } = new List<string>();

    // Çalışma saatleri ve kapalı günler için kompleks tip
    public FacilityHoursDto Hours { get; set; }

    // Tesiste verilen alt hizmetler ve fiyatları
    public List<FacilityServiceItemDto> Services { get; set; } = new List<FacilityServiceItemDto>();
}

public class FacilityHoursDto
{
    // Örn: ["Pzt", "Sal", "Cmt", "Paz"] vb. İngilizce enum da olabilir, frontend Türkçe kısaltmalar ("Cmt") gönderiyor.
    public List<string> ClosedDays { get; set; } = new List<string>();
}

public class FacilityServiceItemDto
{
    // Var olan bir servisi güncelliyorsak Id dolu gelir, yeni eklenen bir servis ise Id null gelir.
    public string Id { get; set; } 
    public string ServiceName { get; set; }
    public decimal Price { get; set; }
}
```

> [!WARNING]
> Tesis güncellenirken DTO içerisindeki `Services` listesi iletirken; backend tarafında tesisin mevcuttaki hizmetleri ile karşılaştırılarak, listede olmayanların veritabanından silinmesi (orphan removal), olanların güncellenmesi ve ID'si boş gelenlerin yeni kayıt olarak eklenmesi (UPSERT mantığı) gerekmektedir. 

### **Örnek JSON İsteği (Request Body)**

```json
{
  "name": "Erkek Kuaförü",
  "category": "Kuafor",
  "appointmentMode": "Appointment",
  "maxConcurrency": 3,
  "openingTime": "08:00",
  "closingTime": "19:00",
  "description": "Erkek saç ve sakal bakım birimi.",
  "image": "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAIBA...",
  "staff": [
    "Ahmet Yılmaz",
    "Mehmet Kaçar"
  ],
  "hours": {
    "closedDays": [
      "Pzt",
      "Sal"
    ]
  },
  "services": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "serviceName": "Saç Kesimi",
      "price": 150.00
    },
    {
      "id": null,
      "serviceName": "Sakal Tıraşı",
      "price": 75.00
    }
  ]
}
```

### **Örnek JSON Yanıtı (200 OK - Response)**

```json
{
  "id": "b18b4562-4537-4f40-a15d-8b012b1d3ef6",
  "ordueviId": "a22b4562-4327-4c40-b12d-12345b1d3e11",
  "name": "Erkek Kuaförü",
  "category": "Kuafor",
  "appointmentMode": "Appointment",
  "openingTime": "08:00",
  "closingTime": "19:00",
  "image": "data:image/jpeg;base64,/9j/4AAQ...",
  "staff": [
    "Ahmet Yılmaz",
    "Mehmet Kaçar"
  ],
  "hours": {
    "closedDays": ["Pzt", "Sal"]
  },
  "services": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "serviceName": "Saç Kesimi",
      "price": 150.00
    },
    {
      "id": "new-uuid-generated-by-db",
      "serviceName": "Sakal Tıraşı",
      "price": 75.00
    }
  ],
  "message": "Tesis başarıyla güncellendi."
}
```

---

## 2. Orduevi Güncelleme Ucu (Endpoint)

Frontend'de orduevleri listeleniyor ve silinebiliyor ancak güncellenmesi için de bir altyapı hazırlanmalıdır. (Zaten Swagger üzerinde de bu endpointin bulunması gerekmektedir).

### **Endpoint Detayları**
- **HTTP Metodu:** `PUT`
- **Route (Yol):** `/api/Orduevleri/{id}`

### **C# DTO Yapısı**

```csharp
public class UpdateOrdueviRequestDto
{
    public string Name { get; set; }
    public string Location { get; set; }
    public string Description { get; set; }
    public string ContactNumber { get; set; }
    public string Address { get; set; }
}
```

### **Örnek JSON İsteği (Request Body)**

```json
{
  "name": "Merkez Ankara Orduevi",
  "location": "Kızılay, Ankara",
  "description": "Ankara merkezdeki en büyük orduevimizdir.",
  "contactNumber": "+90 312 123 45 67",
  "address": "Atatürk Bulvarı No:1"
}
```

### **Örnek JSON Yanıtı (200 OK - Response)**

```json
{
  "id": "a22b4562-4327-4c40-b12d-12345b1d3e11",
  "name": "Merkez Ankara Orduevi",
  "location": "Kızılay, Ankara",
  "description": "Ankara merkezdeki en büyük orduevimizdir.",
  "contactNumber": "+90 312 123 45 67",
  "address": "Atatürk Bulvarı No:1",
  "createdAt": "2024-01-01T10:00:00Z",
  "updatedAt": "2026-04-18T10:45:00Z",
  "message": "Orduevi bilgileri başarıyla güncellendi."
}
```

---

## Backend Geliştiricisine Notlar
1. **Veri Tipleri:** Tesis görselleri "Multipart File" yükleme yerine şimdilik basit olması adına form'un JSON body'sine `Base64` string olarak yedirilmiştir (`image` alanı). İleride bu durum performansı etkilerse ayrı bir resim yükleme (S3/Blob + URL) endpointine taşınabilir.
2. **Koleksiyon Güncellemeleri:** PUT işlemi idempotent olmalıdır. Frontend tarafı, elindeki tablonun "son halini" `services` veya `staff` olarak gönderir. O tesisin DB'de mevcut olan listesi ile gelen liste karşılaştırılmalı (örneğin UI'dan silinen bir servis, istek payload'unda gelmeyeceği için veritabanından Hard Delete veya Soft Delete yapılmalıdır).
3. **Rol İzinleri:** Bu işlemler (`[Authorize(Roles = "Admin")]` gibi) mutlaka yetki doğrulamasına tabii tutulmalıdır.
