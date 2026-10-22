#define LDR_PIN A0
#define BUZZER_PIN 8
#define BUTTON_PIN 2
#define RED_LED 4
#define GREEN_LED 5

// Lazer LDR'ye vururken değer ~40 (Güvenli durum).
// Lazer ışını kesildiğinde değer yükselir.
// Eşik değerini 40'ın üzerinde (örneğin 200) olarak ayarlıyoruz:
const int LAZER_ESIK_DEGERI = 60; 

bool alarmDurumu = false;

void setup() {
  Serial.begin(9600); // C# ile iletişim
  
  pinMode(BUZZER_PIN, OUTPUT);
  pinMode(RED_LED, OUTPUT);
  pinMode(GREEN_LED, OUTPUT);
  pinMode(BUTTON_PIN, INPUT_PULLUP); // Dahili direnç aktif
  
  // Başlangıç durumu: Sistem güvenli
  digitalWrite(GREEN_LED, HIGH);
  digitalWrite(RED_LED, LOW);
  digitalWrite(BUZZER_PIN, LOW);
}

void loop() {
  int ldrDegeri = analogRead(LDR_PIN);

  // 1. C#'tan gelen sıfırlama (RESET) komutunu dinle
  if (Serial.available() > 0) {
    String gelenKomut = Serial.readStringUntil('\n');
    gelenKomut.trim();
    if (gelenKomut == "RESET") {
      sistemiSifirla();
    }
  }

  // 2. Fiziksel Buton İle Sıfırlama
  if (digitalRead(BUTTON_PIN) == LOW && alarmDurumu) {
    sistemiSifirla();
    delay(300); // Debounce engeli
  }

  // 3. İhlal Tespiti (Işın kesilip değer 200'ün üstüne ÇIKTIĞINDA tetiklenir)
  if (ldrDegeri > LAZER_ESIK_DEGERI && !alarmDurumu) {
    alarmDurumu = true;
    
    // Görsel ve Sesli İkaz
    digitalWrite(GREEN_LED, LOW);
    digitalWrite(RED_LED, HIGH);
    digitalWrite(BUZZER_PIN, HIGH);
    
    // C#'a Bilgi Gönder: "ALARM:LDR_DEGERI"
    Serial.println("ALARM:" + String(ldrDegeri));
  }

  delay(100);
}

void sistemiSifirla() {
  alarmDurumu = false;
  digitalWrite(BUZZER_PIN, LOW);
  digitalWrite(RED_LED, LOW);
  digitalWrite(GREEN_LED, HIGH);
  Serial.println("STATUS:NORMAL");
}