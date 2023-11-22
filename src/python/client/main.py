from typing import List
import aiohttp
import asyncio
import uuid


class RoundRobinRetryHandler:
    def __init__(self, endpoints: List[str], api_keys: List[str], delay: int = 10, retries: int = 3):
        self.endpoints: List[str] = endpoints
        self.api_keys: List[str] = api_keys
        self.current_endpoint_index: int = 0
        self.delay: int = delay
        self.retries: int = retries

    async def make_request(self, method, path, data=None, **kwargs) -> str:
        for _ in range(self.retries):
            endpoint = self.endpoints[self.current_endpoint_index % len(
                self.endpoints)]
            api_key = self.api_keys[self.current_endpoint_index % len(
                self.api_keys)]
            headers = {'api-key', api_key}
            async with aiohttp.ClientSession() as session:
                async with session.request(method, endpoint + path, data=data, headers=headers, **kwargs) as response:
                    if response.status not in [429, 500, 502, 503, 504]:
                        return await response.text()

            self.current_endpoint_index += 1
            delay = int(response.headers.get('Retry-After', self.delay))
            await asyncio.sleep(delay)

        raise Exception("Request failed after 3 retries")


apiKey1 = str(uuid.uuid4())
apiKey2 = str(uuid.uuid4())
handler = RoundRobinRetryHandler(
    ["http://localhost:5295/api/v1/endpoint1",
     "http://localhost:5295/api/v1/endpoint1"], [apiKey1, apiKey1])

prompt = {"prompt": "What is the speed of light?"}


async def mainasync():
    while True:
        try:
            response = await handler.make_request(
                "POST", "/api/resource", data=prompt)
            print(response)
        except Exception as e:
            print(e)
        # wait 200ms
        await asyncio.sleep(0.2)


if __name__ == "__main__":
    asyncio.run(mainasync())
