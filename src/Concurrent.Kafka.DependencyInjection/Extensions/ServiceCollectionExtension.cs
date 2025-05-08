
using KafkaFlow;
using KafkaFlow.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SaslMechanism = Confluent.Kafka.SaslMechanism;
using SaslOauthbearerMethod = Confluent.Kafka.SaslOauthbearerMethod;
using SslEndpointIdentificationAlgorithm = Confluent.Kafka.SslEndpointIdentificationAlgorithm;
using SecurityProtocol = Confluent.Kafka.SecurityProtocol;
using Confluent.SchemaRegistry;
using Concurrent.Kafka.Core.Options;

namespace Concurrent.Kafka.Core.DependencyInjection;

public static class ServiceCollectionExtension
{
    /// <summary>
    /// Extends the IServiceCollection with Kafka bus configuration.
    /// </summary>
    /// <param name="services">The IServiceCollection to configure.</param>
    /// <param name="options">The KafkaOptions containing configuration settings.</param>
    /// <param name="clusterAction">An action to configure the cluster.</param>
    /// <returns>The configured IServiceCollection.</returns>
    /// <exception cref="Exception">Thrown when no server URLs are configured in the options.</exception>
    public static IServiceCollection AddKafkaBus(
        this IServiceCollection services,
        KafkaOptions options,
        Action<IClusterConfigurationBuilder> clusterAction
        )
    {
        if (options.ServerUrls.Length == 0)
            throw new Exception("No Server URLs configured.");


        var schemaRegistryConfig = options.SchemaRegistryConfig;
        var securityConfig = options.AuthenticationConfig;

        if (options.EnableHealthChecks)
        {
            _ = services.AddHealthChecks()
                .AddKafka(
                    config => MapHealthCheckConfig(ref config, options, securityConfig),
                    topic: options.HealthCheckTopic,
                    name: "Kafka Healthcheck",
                    tags: ["kafka"]
                );
        }

        services.AddKafka(kafka =>
        {
            #region OpenTelemetry
            if (options.EnableTracing)
            {
                kafka.AddOpenTelemetryInstrumentation(instrumentationOptions =>
                {
                    instrumentationOptions.EnrichConsumer = (activity, messageContext) =>
                    {
                        activity.SetTag("messaging.destination.group.id", messageContext.ConsumerContext.GroupId);
                    };
                });
            }
            #endregion

            kafka.UseMicrosoftLog();
            kafka.AddCluster(cluster =>
            {
                #region Admininstration Stuff
                if (options.EnableAdmin)
                    cluster.EnableAdminMessages(options.AdminMessageTopic)
                           .EnableTelemetry(options.TelemetryTopic);

                #endregion
                #region SchemRegistry
                if (schemaRegistryConfig != null)
                {
                    cluster.WithSchemaRegistry(config => MapSchemaRegistry(ref config, schemaRegistryConfig));
                }
                #endregion

                #region Authentication

                if (securityConfig != null)
                {
                    cluster.WithSecurityInformation(config => MapSecurityInfo(ref config, securityConfig));
                }
                #endregion

                cluster.WithBrokers(options.ServerUrls);
                clusterAction.Invoke(cluster);
            });
        });

        if (options.EnableAdmin)
            services.AddControllers();

        return services;
    }

    private static void MapSecurityInfo(ref SecurityInformation information, SecurityInformation securityConfig)
    {
        information.SaslMechanism = securityConfig.SaslMechanism;
        information.SaslPassword = securityConfig.SaslPassword;
        information.SaslUsername = securityConfig.SaslUsername;
        information.SecurityProtocol = securityConfig.SecurityProtocol;

        information.SaslOauthbearerConfig = securityConfig.SaslOauthbearerConfig;
        information.SaslOauthbearerMethod = securityConfig.SaslOauthbearerMethod;
        information.SaslOauthbearerScope = securityConfig.SaslOauthbearerScope;
        information.SaslOauthbearerClientId = securityConfig.SaslOauthbearerClientId;
        information.SaslOauthbearerClientSecret = securityConfig.SaslOauthbearerClientSecret;

        information.SaslKerberosKeytab = securityConfig.SaslKerberosKeytab;
        information.SaslKerberosPrincipal = securityConfig.SaslKerberosPrincipal;
        information.SaslKerberosKinitCmd = securityConfig.SaslKerberosKinitCmd;
        information.SaslKerberosServiceName = securityConfig.SaslKerberosServiceName;
        information.SaslKerberosMinTimeBeforeRelogin = securityConfig.SaslKerberosMinTimeBeforeRelogin;

        information.EnableSslCertificateVerification = securityConfig.EnableSslCertificateVerification;
        information.SslCaLocation = securityConfig.SslCaLocation;
        information.SslCaPem = securityConfig.SslCaPem;
        information.SslCertificateLocation = securityConfig.SslCertificateLocation;
        information.SslCertificatePem = securityConfig.SslCertificatePem;
        information.SslKeystoreLocation = securityConfig.SslKeystoreLocation;
        information.SslCipherSuites = securityConfig.SslCipherSuites;
        information.SslCrlLocation = securityConfig.SslCrlLocation;
        information.SslCurvesList = securityConfig.SslCurvesList;
        information.SslEndpointIdentificationAlgorithm = securityConfig.SslEndpointIdentificationAlgorithm;
    }

    private static void MapSchemaRegistry(ref SchemaRegistryConfig config, SchemaRegistryConfig schemaRegistryConfig)
    {
        config.Url = schemaRegistryConfig.Url ?? null;
        config.RequestTimeoutMs = schemaRegistryConfig.RequestTimeoutMs ?? 5000;
        config.MaxCachedSchemas = schemaRegistryConfig.MaxCachedSchemas ?? 12;
        config.SslCaLocation = schemaRegistryConfig.SslCaLocation ?? string.Empty;
        config.SslKeystoreLocation = schemaRegistryConfig.SslKeystoreLocation ?? string.Empty;
        config.BasicAuthCredentialsSource = schemaRegistryConfig.BasicAuthCredentialsSource;
        config.BasicAuthUserInfo = schemaRegistryConfig.BasicAuthUserInfo;
        config.EnableSslCertificateVerification = schemaRegistryConfig.EnableSslCertificateVerification ?? false;
    }

    private static void MapHealthCheckConfig(ref Confluent.Kafka.ProducerConfig config, KafkaOptions options, SecurityInformation? securityConfig)
    {
        config.BootstrapServers = string.Join(",", options.ServerUrls);
        if (securityConfig != null)
        {
            config.SaslMechanism = (SaslMechanism?)securityConfig.SaslMechanism;
            config.SaslPassword = securityConfig.SaslPassword;
            config.SaslUsername = securityConfig.SaslUsername;
            config.SecurityProtocol = (SecurityProtocol?)securityConfig.SecurityProtocol;

            config.SaslOauthbearerConfig = securityConfig.SaslOauthbearerConfig;
            config.SaslOauthbearerMethod = (SaslOauthbearerMethod?)securityConfig.SaslOauthbearerMethod;
            config.SaslOauthbearerScope = securityConfig.SaslOauthbearerScope;
            config.SaslOauthbearerClientId = securityConfig.SaslOauthbearerClientId;
            config.SaslOauthbearerClientSecret = securityConfig.SaslOauthbearerClientSecret;

            config.SaslKerberosKeytab = securityConfig.SaslKerberosKeytab;
            config.SaslKerberosPrincipal = securityConfig.SaslKerberosPrincipal;
            config.SaslKerberosKinitCmd = securityConfig.SaslKerberosKinitCmd;
            config.SaslKerberosServiceName = securityConfig.SaslKerberosServiceName;
            config.SaslKerberosMinTimeBeforeRelogin = securityConfig.SaslKerberosMinTimeBeforeRelogin;

            config.EnableSslCertificateVerification = securityConfig.EnableSslCertificateVerification;
            config.SslCaLocation = securityConfig.SslCaLocation;
            config.SslCaPem = securityConfig.SslCaPem;
            config.SslCertificateLocation = securityConfig.SslCertificateLocation;
            config.SslCertificatePem = securityConfig.SslCertificatePem;
            config.SslKeystoreLocation = securityConfig.SslKeystoreLocation;
            config.SslCipherSuites = securityConfig.SslCipherSuites;
            config.SslCrlLocation = securityConfig.SslCrlLocation;
            config.SslCurvesList = securityConfig.SslCurvesList;
            config.SslEndpointIdentificationAlgorithm = (SslEndpointIdentificationAlgorithm?)securityConfig.SslEndpointIdentificationAlgorithm;

        }
    }
}