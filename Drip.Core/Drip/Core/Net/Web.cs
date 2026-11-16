using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Drip.Core.Net;

public static class Web
{
	private sealed class FallbackHandler : DelegatingHandler
	{
		private readonly HttpMessageHandler _direct = new SocketsHttpHandler
		{
			UseProxy = false
		};

		public FallbackHandler()
			: base(new SocketsHttpHandler())
		{
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			try
			{
				return await base.SendAsync(request, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (HttpRequestException ex) when (ex.InnerException is SocketException)
			{
				try
				{
					using HttpRequestMessage again = await Clone(request).ConfigureAwait(continueOnCapturedContext: false);
					return await new HttpMessageInvoker(_direct, disposeHandler: false).SendAsync(again, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (HttpRequestException ex2)
				{
					string value = request.RequestUri?.Host ?? "the server";
					throw new HttpRequestException($"HttpRequestException: could not connect to {value}. The system proxy refused the connection and a direct connection failed too ({Code(ex)} / {Code(ex2)}).", ex2);
				}
			}
		}

		private static string Code(HttpRequestException e)
		{
			if (!(e.InnerException is SocketException { SocketErrorCode: var socketErrorCode }))
			{
				return "no connection";
			}
			return socketErrorCode.ToString();
		}

		private static async Task<HttpRequestMessage> Clone(HttpRequestMessage r)
		{
			HttpRequestMessage c = new HttpRequestMessage(r.Method, r.RequestUri)
			{
				Version = r.Version
			};
			foreach (KeyValuePair<string, IEnumerable<string>> header in r.Headers)
			{
				c.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
			if (r.Content != null)
			{
				c.Content = new ByteArrayContent(await r.Content.ReadAsByteArrayAsync().ConfigureAwait(continueOnCapturedContext: false));
				foreach (KeyValuePair<string, IEnumerable<string>> header2 in r.Content.Headers)
				{
					c.Content.Headers.TryAddWithoutValidation(header2.Key, header2.Value);
				}
			}
			return c;
		}
	}

	public static HttpClient CreateClient(TimeSpan timeout)
	{
		return new HttpClient(new FallbackHandler(), disposeHandler: true)
		{
			Timeout = timeout
		};
	}
}
