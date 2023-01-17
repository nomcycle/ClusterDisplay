using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad.Services
{
    public static class ConfigServiceExtension
    {
        public static void AddConfigService(this IServiceCollection services)
        {
            services.AddSingleton<ConfigService>();
        }
    }

    /// <summary>
    /// Service managing the main configuration of the REST service (that can be configured remotely through REST).
    /// </summary>
    public class ConfigService
    {
        public ConfigService(IConfiguration configuration, ILogger<ConfigService> logger,
                             StatusService statusService)
        {
            m_Logger = logger;
            m_StatusService = statusService;

            // We use one setting from the "static" configuration, the location of where to get our own config.json
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            m_PersistPath = Path.GetFullPath(configuration["configPath"], assemblyFolder!);

            Initialize();
            m_InitialEndPoints = m_Config.ControlEndPoints.ToArray();

            ValidateNew += ValidateNewConfig;
            Changed += ConfigChanged;
        }

        /// <summary>
        /// Returns the endpoint without having to create the ConfigService.
        /// </summary>
        /// <param name="configuration">.Net configuration coming from various sources (static, does not change
        /// during the execution of the application, often comes from appsettings.json).</param>
        /// <returns>The list of endpoints</returns>
        /// <remarks>Would be nice to simply use <see cref="ConfigService.Current"/>, but for this we need to create
        /// an instance and for this we need dependency injection to be up and running.  However, it is not since we
        /// need to configure the endpoints before starting the service...</remarks>
        public static string[] GetEndpoints(IConfiguration configuration)
        {
            var assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var configPath = Path.GetFullPath(configuration["configPath"], assemblyFolder!);
            try
            {
                if (File.Exists(configPath))
                {
                    using var loadStream = File.OpenRead(configPath);
                    var config = JsonSerializer.Deserialize<Config>(loadStream, Json.SerializerOptions);
                    return config.ControlEndPoints.ToArray();
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return new[] { k_DefaultEndpoint };
        }

        /// <summary>
        /// Current service's configuration.
        /// </summary>
        public Config Current
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Config;
                }
            }
        }

        /// <summary>
        /// Changes the current configuration to the new provided one.
        /// </summary>
        /// <param name="newConfig">New configuration</param>
        /// <returns>List of reasons why it was rejected (or an empty list if changes was done with success).</returns>
        public async Task<IEnumerable<string>> SetCurrentAsync(Config newConfig)
        {
            // Wait for any other setting of the config currently going on to be completed before continuing.
            TaskCompletionSource? setCurrentCompleteTcs = null;
            while (setCurrentCompleteTcs == null)
            {
                Task? toWaitOn = null;
                lock (m_Lock)
                {
                    if (m_ExecutingSetCurrent == null)
                    {
                        setCurrentCompleteTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        m_ExecutingSetCurrent = setCurrentCompleteTcs.Task;
                    }
                    else
                    {
                        toWaitOn = m_ExecutingSetCurrent;
                    }
                }
                if (toWaitOn != null)
                {
                    await toWaitOn;
                }
            }

            try
            {
                // Validate the new configuration
                try
                {
                    var configChange = new ConfigChangeSurvey(newConfig);
                    ValidateNew?.Invoke(configChange);
                    if (!configChange.Accepted)
                    {
                        return configChange.RejectReasons;
                    }
                }
                catch(Exception e)
                {
                    return new[] { e.ToString() };
                }

                // Make the change
                lock (m_Lock)
                {
                    m_Config = newConfig;
                }

                // Broadcast the news
                try
                {
                    var tasks = Changed?.GetInvocationList().Select(d => ((Func<Task>)d)());
                    if (tasks != null && tasks.Any())
                    {
                        await Task.WhenAll(tasks);
                    }
                }
                catch(Exception e)
                {
                    m_Logger.LogError(e, "Exception informing about an accepted configuration change, sate of some " +
                                      "services might be out of sync and a restart of the HangarBay should be done");
                    m_StatusService.SignalPendingRestart();
                }

                // And save and return
                Save();

                // Done
                return Enumerable.Empty<string>();
            }
            finally
            {
                lock (m_Lock)
                {
                    Debug.Assert(m_ExecutingSetCurrent == setCurrentCompleteTcs.Task);
                    m_ExecutingSetCurrent = null;
                    setCurrentCompleteTcs.SetResult();
                }
            }
        }

        /// <summary>
        /// Small class providing information about an upcoming configuration change.
        /// </summary>
        public class ConfigChangeSurvey
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="proposed">Newly proposed configuration</param>
            public ConfigChangeSurvey(Config proposed)
            {
                Proposed = proposed;
            }

            /// <summary>
            /// New proposed configuration
            /// </summary>
            public Config Proposed { get; private set; }

            /// <summary>
            /// Is the configuration change accepted by everyone?
            /// </summary>
            public bool Accepted => !m_RejectReasons.Any();

            /// <summary>
            /// Reasons why the new configuration is not acceptable.
            /// </summary>
            public IReadOnlyList<string> RejectReasons => m_RejectReasons;

            /// <summary>
            /// Reject the new current configuration
            /// </summary>
            /// <param name="reason">Reject reasons</param>
            public void Reject(string reason) { m_RejectReasons.Add(reason); }

            /// <summary>
            /// Reasons why the new configuration is not acceptable.
            /// </summary>
            List<string> m_RejectReasons = new();
        }

        /// <summary>
        /// Event fired to anybody that want to validate a new configuration (and potentially reject it before it gets
        /// applied).
        /// </summary>
        public event Action<ConfigChangeSurvey>? ValidateNew;

        /// <summary>
        /// Indicate that the configuration changed.
        /// </summary>
        public event Func<Task>? Changed;

        /// <summary>
        /// Initialize the service
        /// </summary>
        void Initialize()
        {
            bool configLoaded = false;
            try
            {
                if (File.Exists(m_PersistPath))
                {
                    using (var loadStream = File.OpenRead(m_PersistPath))
                    {
                        m_Config = JsonSerializer.Deserialize<Config>(loadStream, Json.SerializerOptions);
                    }
                    m_Logger.LogInformation("Loaded configuration from {PersistPath}", m_PersistPath);
                    configLoaded = true;
                }
                else
                {
                    m_Logger.LogInformation("Starting from a brand new default configuration");
                }
            }
            catch (Exception e)
            {
                m_Logger.LogInformation(e, "Failed to load {PersistPath}, will use the default configuration",
                    m_PersistPath);
            }

            if (!configLoaded)
            {
                m_Config = new();
                m_Config.Identifier = Guid.NewGuid();
                m_Config.ControlEndPoints = new[] { k_DefaultEndpoint };
                // Try to guess the default "cluster network".  Most likely not the right one and it will have to be
                // changed, but let's try one...
                var clusterNetwork = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && !n.Name.ToLower().Contains("vpn") &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .MaxBy(n => n.Speed);
                if (clusterNetwork != null)
                {
                    m_Config.ClusterNetworkNic = clusterNetwork.Name;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(m_PersistPath)!);
                Save();
            }
        }

        /// <summary>
        /// Save the current configuration.
        /// </summary>
        void Save()
        {
            try
            {
                using FileStream serializeStream = File.Create(m_PersistPath);
                JsonSerializer.Serialize(serializeStream, Current, Json.SerializerOptions);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to save updated configuration to {PersistPath}", m_PersistPath);
            }
        }

        /// <summary>
        /// Validate a new configuration
        /// </summary>
        /// <param name="newConfig">Information about the new configuration</param>
        /// <remarks>Normally we want every service to validate their "own part" of the configuration, however some
        /// parts are not really owned by any actual services (like control endpoints).</remarks>
        void ValidateNewConfig(ConfigChangeSurvey newConfig)
        {
            if (newConfig.Proposed.Identifier != m_Config.Identifier)
            {
                newConfig.Reject("LaunchPad identifier cannot be changed.");
                return;
            }

            foreach (var endpoint in newConfig.Proposed.ControlEndPoints)
            {
                var validationMessage = EndPointHelpers.ValidateLocal(endpoint);
                if (validationMessage.Length > 0)
                {
                    newConfig.Reject(validationMessage);
                    return;
                }
            }
        }

        /// <summary>
        /// React to configuration changes
        /// </summary>
        /// <remarks>Normally we want every service to deal with changes in their "own part" of the configuration,
        /// however some parts are not really owned by any actual services (like control endpoints).</remarks>
        Task ConfigChanged()
        {
            if (!m_Config.ControlEndPoints.SequenceEqual(m_InitialEndPoints))
            {
                m_StatusService.SignalPendingRestart();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Default endpoint
        /// </summary>
        const string k_DefaultEndpoint = "http://0.0.0.0:8200";

        readonly ILogger<ConfigService> m_Logger;
        readonly StatusService m_StatusService;

        /// <summary>
        /// Where the configuration was from and where to save it.
        /// </summary>
        string m_PersistPath;

        /// <summary>
        /// <see cref="Config.ControlEndPoints"/> at startup.
        /// </summary>
        readonly string[] m_InitialEndPoints;

        /// <summary>
        /// Used to synchronize access to the member variables below.
        /// </summary>
        readonly object m_Lock = new();

        /// <summary>
        /// Task representing the currently executing <see cref="SetCurrentAsync(Config)"/>, used to serialize concurrent
        /// calls to the method.
        /// </summary>
        Task? m_ExecutingSetCurrent;

        /// <summary>
        /// The actual configuration being transmitted and persisted.
        /// </summary>
        Config m_Config;
    }
}