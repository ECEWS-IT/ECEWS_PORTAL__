using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace EcewsPortal.Services;

public static class WeatherServiceRegistration
{
	public static IServiceCollection AddWeatherService(this IServiceCollection services)
	{
		services.AddHttpClient<IWeatherService, WeatherService>((HttpClient client) =>
		{
			client.Timeout = TimeSpan.FromSeconds(20L);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("ECEWS-Portal/1.0");
		}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
		{
			SslOptions = new SslClientAuthenticationOptions
			{
				CertificateRevocationCheckMode = X509RevocationMode.NoCheck
			},
			PooledConnectionLifetime = TimeSpan.FromMinutes(5L)
		});
		return services;
	}
}
