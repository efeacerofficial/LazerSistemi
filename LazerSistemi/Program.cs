using System;
using System.IO;                  // Dosya varlık kontrolü için
using System.IO.Ports;
using System.Data.SQLite;        // SQLite kütüphanesi
using System.Threading;          // Bekleme süreleri için
using System.Threading.Tasks;    // Arka plan döngüsü için
using System.Runtime.InteropServices; // Windows Çekirdek API (Garantili Ses Frekansı İçin)

class Program
{
    // Windows Çekirdeğinden doğrudan Ses Frekansı Çalma Metodu (Garantili "DİN DİN DİN" Sesi)
    [DllImport("kernel32.dll")]
    public static extern bool Beep(int dwFreq, int dwDuration);

    private static readonly string DbFileName = "guvenlik_otomasyon.db";
    private static readonly string DbConnectionString = $"Data Source={DbFileName};Version=3;";
    private static SerialPort _serialPort;

    // Alarm aktiflik durumu
    private static bool _isAlarmActive = false;

    static void Main(string[] args)
    {
        Console.Title = "Lazer Güvenlik Otomasyonu & SQLite Kontrol Paneli";

        // 1. Veritabanı ve Tabloyu Otomatik Kur
        VeritabaniniVeTabloyuOtomatikKur();

        // 2. Seri Port Ayarları (Arduino COM Portunuzu Yazın)
        _serialPort = new SerialPort("COM6", 9600);
        _serialPort.DataReceived += SerialPort_DataReceived;

        try
        {
            _serialPort.Open();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================");
            Console.WriteLine("   LAZER GÜVENLİK OTOMASYONU VE VERİTABANI       ");
            Console.WriteLine("=================================================");
            Console.ResetColor();
            Console.WriteLine(" [R] - Sistemi Sıfırla (Arduino'ya RESET Gönder)");
            Console.WriteLine(" [L] - Veritabanındaki Geçmiş Logları Listele (SQL SELECT)");
            Console.WriteLine(" [Q] - Çıkış\n");

            while (true)
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.R)
                {
                    SistemiSifirla();
                }
                else if (key == ConsoleKey.L)
                {
                    Console.WriteLine("\n--- VERİTABANINDAKİ TÜM KAYITLAR ---");
                    LoglariListele();
                    Console.WriteLine("------------------------------------\n");
                }
                else if (key == ConsoleKey.Q)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[HATA] Seri Port Bağlantı Hatası: {ex.Message}");
            Console.ResetColor();
            Console.ReadLine();
        }
    }

    private static void SistemiSifirla()
    {
        _isAlarmActive = false; // Ses döngüsünü anında durdur
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.WriteLine("RESET");
            Console.WriteLine("\n[C# -> ARDUINO] Sıfırlama emri gönderildi.");
        }
    }

    #region Seri Port Dinleyici ve Ritmik "DİN DİN DİN" Frekans Sesi
    private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string gelenVeri = _serialPort.ReadLine();
            if (string.IsNullOrWhiteSpace(gelenVeri)) return; // Alt alta boş satır girmesini engeller
            gelenVeri = gelenVeri.Trim();

            // Arduino Işın Kesildiğinde "ALARM:xxx" Gönderir
            if (gelenVeri.StartsWith("ALARM:"))
            {
                if (!_isAlarmActive)
                {
                    _isAlarmActive = true;

                    string ldrValStr = gelenVeri.Replace("ALARM:", "");
                    int ldrVal = 0;
                    int.TryParse(ldrValStr, out ldrVal);

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ALARM İHLALİ!] Lazer Kesildi! LDR Değeri: {ldrVal} - Zaman: {DateTime.Now:HH:mm:ss}");
                    Console.ResetColor();

                    // RİTMİK "DİN DİN DİN" FREKANS SESİ DÖNGÜSÜ (Arka Planda)
                    Task.Run(() => FrekansAlarmSesiDongusu());

                    // SQL INSERT
                    LogKaydet("IHLAL", ldrVal);
                }
            }
            // Arduino Sıfırlandığında "STATUS:NORMAL" Gönderir
            else if (gelenVeri == "STATUS:NORMAL")
            {
                if (_isAlarmActive)
                {
                    _isAlarmActive = false; // Sesi kapatır

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[SİSTEM NORMAL] Güvenlik Çizgisi Aktif.");
                    Console.ResetColor();

                    // SQL INSERT
                    LogKaydet("SIFIRLANDI", 0);
                }
            }
        }
        catch { }
    }

    // Doğrudan Windows Hoparlör Sürücüsünden Frekans Sesi (Din Din Din)
    private static void FrekansAlarmSesiDongusu()
    {
        while (_isAlarmActive)
        {
            try
            {
                // 1. "DİN" Tonu (1800 Hz Frekans - 150 ms Süre)
                Beep(1800, 150);
                Thread.Sleep(80); // Ritim Aralığı

                if (!_isAlarmActive) break;

                // 2. "DİN" Tonu (2200 Hz Frekans - 150 ms Süre)
                Beep(2200, 150);
                Thread.Sleep(80); // Ritim Aralığı

                if (!_isAlarmActive) break;

                // 3. "DİN" Tonu (2600 Hz Frekans - 150 ms Süre)
                Beep(2600, 150);
                Thread.Sleep(200); // Döngüler arası kısa ara
            }
            catch
            {
                // Olası bir hata durumunda standart bip at
                Console.Beep();
                Thread.Sleep(300);
            }
        }
    }
    #endregion

    #region Veritabanı İşlemleri (SQL)
    private static void VeritabaniniVeTabloyuOtomatikKur()
    {
        try
        {
            if (!File.Exists(DbFileName))
            {
                SQLiteConnection.CreateFile(DbFileName);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[SQL] '{DbFileName}' veritabanı dosyası oluşturuldu!");
                Console.ResetColor();
            }

            using (var connection = new SQLiteConnection(DbConnectionString))
            {
                connection.Open();

                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS SecurityLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EventType TEXT NOT NULL,
                        LdrValue INTEGER NOT NULL,
                        LogTime DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";

                using (var command = new SQLiteCommand(createTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SQL] Veritabanı ve 'SecurityLogs' tablosu hazır.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HATA] Veritabanı kurulurken hata oluştu: {ex.Message}");
        }
    }

    private static void LogKaydet(string eventType, int ldrValue)
    {
        try
        {
            using (var connection = new SQLiteConnection(DbConnectionString))
            {
                connection.Open();

                string insertSql = @"
                    INSERT INTO SecurityLogs (EventType, LdrValue, LogTime) 
                    VALUES (@eventType, @ldrValue, @logTime);";

                using (var command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@eventType", eventType);
                    command.Parameters.AddWithValue("@ldrValue", ldrValue);
                    command.Parameters.AddWithValue("@logTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    command.ExecuteNonQuery();
                }
            }
        }
        catch { }
    }

    private static void LoglariListele()
    {
        try
        {
            using (var connection = new SQLiteConnection(DbConnectionString))
            {
                connection.Open();

                string selectSql = "SELECT Id, EventType, LdrValue, LogTime FROM SecurityLogs ORDER BY Id DESC LIMIT 15;";

                using (var command = new SQLiteCommand(selectSql, connection))
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("Veritabanında henüz kayıt bulunmuyor.");
                        return;
                    }

                    Console.WriteLine($"{"ID",-5} | {"OLAY TİPİ",-12} | {"LDR DEĞERİ",-10} | {"TARİH SAAT",-20}");
                    Console.WriteLine(new string('-', 55));

                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string eventType = reader.GetString(1);
                        int ldrValue = reader.GetInt32(2);
                        string logTime = reader.GetString(3);

                        Console.WriteLine($"{id,-5} | {eventType,-12} | {ldrValue,-10} | {logTime,-20}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HATA] Loglar okunurken hata: {ex.Message}");
        }
    }
    #endregion
}