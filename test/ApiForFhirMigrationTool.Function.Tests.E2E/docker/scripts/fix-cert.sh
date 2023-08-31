
#!/bin/sh

# Download and install the emulator cert so the fhir server can communicate with TLS/SSL
# https://docs.microsoft.com/en-us/azure/cosmos-db/linux-emulator?tabs=ssl-netstd21#run-on-macos

echo "Polling CosmosDb emulator for readiness..."

while true; do
    HTTPD=$(wget --no-check-certificate --server-response https://cosmos:8081/_explorer/emulator.pem 2>&1 | awk '/^  HTTP/{print $2}')
    if echo "$HTTPD" | grep -q -E '\b200\b'; then
        break
    fi
    echo "Waiting as status $HTTPD encountered"
    sleep 3
done

echo "CosmosDb emulator is up. Pulling the emulator certificate now..."

wget --no-check-certificate -O ~/emulatorcert.crt https://cosmos:8081/_explorer/emulator.pem

echo "Installing emulator certificate now..."

cp ~/emulatorcert.crt /usr/local/share/ca-certificates/
update-ca-certificates