import asyncio, time, ssl, os, requests
from picows import ws_connect, WSFrame, WSTransport, WSListener, WSMsgType, WSCloseCode

ROOT_CA_PATH = os.getenv("ROOT_CA_PATH", "/certs/root.pem")
API_URL = os.getenv("API_URL", "berg.localhost")
API_USER = os.getenv("API_USER", "4a20727b-5309-56cf-8145-1e1c24fd2cc5")
API_KEY = os.getenv("API_KEY", None)
if not API_KEY:
    raise Exception("API_KEY missing")

j = requests.post(f"https://{API_URL}/api/openid/token", data={
    "client_id": "berg-client",
    "grant_type": "password",
    "username": API_USER,
    "password": API_KEY
}, verify=ROOT_CA_PATH).json()
access_token = j["access_token"]

TARGET_URL = f"wss://{API_URL}/api/events?access_token={access_token}"

class ClientListener(WSListener):
    i = 0

    def on_ws_connected(self, transport: WSTransport):
        print("Connected")
        transport.send(WSMsgType.TEXT, b'{"message":"invalid-data","type":"invalid-type"} ')

    def on_ws_frame(self, transport: WSTransport, frame: WSFrame):
        print(f"Echo reply ({self.i}): {frame.get_payload_as_ascii_text()}")
        transport.send(WSMsgType.TEXT, (f'{{"type":"ping","message":{self.i+1}}}').encode())
        self.i += 1
        if self.i > 100:
            transport.send_close(WSCloseCode.OK)
            transport.disconnect()


async def conn():
    global TARGET_URL
    ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ssl_context.load_verify_locations(ROOT_CA_PATH)
    transport, client = await ws_connect(ClientListener, TARGET_URL, ssl_context=ssl_context)
    await transport.wait_disconnected()

async def main():
    print("Starting")
    conns = [conn() for i in range(10000)]
    start = time.time()
    await asyncio.gather(
        *conns
    )
    print(f"Took {time.time()-start}s")

if __name__ == '__main__':
    asyncio.run(main())