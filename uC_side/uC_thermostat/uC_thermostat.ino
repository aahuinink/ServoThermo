/*
 Basic ESP8266 MQTT example
 This sketch demonstrates the capabilities of the pubsub library in combination
 with the ESP8266 board/library.
 It connects to an MQTT server then:
  - publishes "hello world" to the topic "outTopic" every two seconds
  - subscribes to the topic "inTopic", printing out any messages
    it receives. NB - it assumes the received payloads are strings not binary
  - If the first character of the topic "inTopic" is an 1, switch ON the ESP Led,
    else switch it off
 It will reconnect to the server if the connection is lost using a blocking
 reconnect function. See the 'mqtt_reconnect_nonblocking' example for how to
 achieve the same result without blocking the main loop.
 To install the ESP8266 board, (using Arduino 1.6.4+):
  - Add the following 3rd party board manager under "File -> Preferences -> Additional Boards Manager URLs":
       http://arduino.esp8266.com/stable/package_esp8266com_index.json
  - Open the "Tools -> Board -> Board Manager" and click install for the ESP8266"
  - Select your ESP8266 in "Tools -> Board"
*/

#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include "DHT.h"
#include <Stepper.h>
#include <math.h>

#define INTOPIC "ecet230/AaronH/MOSI"
#define OUTTOPIC "ecet230/AaronH/MISO"
#define TOTALSTEPS 100
#define PIN1 0
#define PIN2 1
#define THERMO_MIN 15
#define THERMO_MAX 30
#define DHTPIN 4
#define DHTTYPE DHT11
// Update these with values suitable for your network.

const char* ssid = "All My Homies Hate Rogers";
const char* password = "fuckRogers!123";
const char* mqtt_server = "broker.emqx.io";

int SelectedTemp = 22;
int CurrentTemp;
long int start;

Stepper motor = Stepper(TOTALSTEPS, PIN1, PIN2);
DHT dht(DHTPIN, DHTTYPE);
WiFiClient espClient;
PubSubClient client(espClient);

void setup_wifi() {

  delay(10);
  // We start by connecting to a WiFi network
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  randomSeed(micros());

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
}

void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] ");
  for (int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();
  StaticJsonDocument<200> doc;
  DeserializationError error = deserializeJson(doc, payload);
  if (error)
  {
    return;
  }
  else
  {
    int query = doc["Query"];
    int SetTemp = doc["SetTemp"];

    if (query > 0)
    {
      char* payload = "###00";
      payload[3] += SelectedTemp / 10;
      payload[4] += SelectedTemp % 10;
      client.publish(OUTTOPIC, payload, false);
      delay(30);
      CurrentTemp = dht.readTemperature();
      String current = String(CurrentTemp,1);
      client.publish(OUTTOPIC, payload, false);
    }
    if (SetTemp > 0)
    {
      UpdateThermostat(SetTemp);
    }
  }
}

void reconnect() {
  // Loop until we're reconnected
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    // Create a random client ID
    String clientId = "ESP8266Client-";
    clientId += String(random(0xffff), HEX);
    // Attempt to connect
    if (client.connect(clientId.c_str())) {
      Serial.println("connected");
      // Once connected, publish an announcement...
      client.publish(OUTTOPIC, "hello world");
      // ... and resubscribe
      client.subscribe(INTOPIC);
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 5 seconds");
      // Wait 5 seconds before retrying
      delay(5000);
    }
  }
}

void setup() {
  Serial.begin(115200);
  setup_wifi();
  client.setServer(mqtt_server, 1883);
  delay(10000);
  client.setCallback(callback);
  dht.begin();
  motor.setSpeed(30);
  // int steps = TOTALSTEPS*(SelectedTemp - THERMO_MIN)/(THERMO_MAX-THERMO_MIN);
  // motor.step(steps);
  start = millis();
  Serial.println("Sending Temp");
  SendTemp();
}

void loop() {

  if (!client.connected()) {
    reconnect();
  }
  client.loop();
  if (millis() - start > 30000){
    start = millis();
    SendTemp();
  }
}


void UpdateThermostat(int newTemp)
{
  int currentSteps = TOTALSTEPS*(SelectedTemp - THERMO_MIN)/(THERMO_MAX-THERMO_MIN);
  int newSteps = TOTALSTEPS*(newTemp - THERMO_MIN)/(THERMO_MAX-THERMO_MIN);
  motor.step(newSteps - currentSteps);
}

void SendTemp()
{
  CurrentTemp = (int)dht.readTemperature();
  Serial.println("Temp read OK");
  if(isnan(CurrentTemp)){
    Serial.println("Was NaN");
    return;
  }
    char* payload = "00";
    payload[0] += CurrentTemp / 10;
    payload[1] += CurrentTemp % 10;
    client.publish(OUTTOPIC, payload);
    Serial.println("publish OK");
}
