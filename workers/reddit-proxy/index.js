export default {
  async fetch(request) {
    const url = new URL(request.url);
    const redditUrl = `https://www.reddit.com${url.pathname}${url.search}`;

    const response = await fetch(redditUrl, {
      headers: {
        "User-Agent": "ai-pulse/1.0 (cloudflare-worker)",
        "Accept": "application/json",
      },
    });

    return new Response(response.body, {
      status: response.status,
      headers: response.headers,
    });
  },
};
