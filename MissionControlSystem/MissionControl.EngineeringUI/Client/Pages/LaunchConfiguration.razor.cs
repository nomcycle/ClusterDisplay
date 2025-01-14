using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Pages
{
    // ReSharper disable once ClassNeverInstantiated.Global -> Instantiated through routing
    public partial class LaunchConfiguration : IDisposable
    {
        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        AssetsService AssetsService { get; set; } = default!;
        [Inject]
        LaunchConfigurationService LaunchConfigurationService { get; set; } = default!;
        [Inject]
        ComplexesService Complexes { get; set; } = default!;
        [Inject]
        MissionCommandsService MissionCommandsService { get; set; } = default!;
        [Inject]
        MissionsService MissionsService { get; set; } = default!;
        [Inject]
        NotificationService NotificationService { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        ILogger<LaunchConfiguration> Logger { get; set; } = default!;


        /// <summary>
        /// <see cref="LaunchCatalog.LaunchParameter"/> of the <see cref="Launchable"/> of the <see cref="LaunchPad"/>s.
        /// </summary>
        IEnumerable<LaunchCatalog.LaunchParameter> m_LaunchablesLaunchParameters = Enumerable.Empty<LaunchCatalog.LaunchParameter>();

        /// <summary>
        /// Tracks the <see cref="Asset"/> and <see cref="Launchable"/>(s) that <see cref="m_LaunchablesLaunchParameters"/> applies to.
        /// </summary>
        (Guid assetId, IEnumerable<string> launchableNames) m_CurrentLaunchables = (default, Enumerable.Empty<string>());

        protected override void OnInitialized()
        {
            MissionControlStatus.ObjectChanged += MissionControlStatusChanged;
            AssetsService.Collection.SomethingChanged += AssetsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged += LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged += LaunchConfigurationChanged;
            Complexes.Collection.SomethingChanged += ComplexesChanged;
            MissionsService.Collection.SomethingChanged += ComplexesChanged;

            SyncParameterValuesToWorkLaunchConfiguration();
        }

        public void Dispose()
        {
            MissionControlStatus.ObjectChanged -= MissionControlStatusChanged;
            AssetsService.Collection.SomethingChanged -= AssetsChanged;
            LaunchConfigurationService.ReadOnlyMissionControlValue.ObjectChanged -= LaunchConfigurationChanged;
            LaunchConfigurationService.WorkValue.ObjectChanged -= LaunchConfigurationChanged;
            Complexes.Collection.SomethingChanged -= ComplexesChanged;
        }

        IEnumerable<LaunchCatalog.LaunchParameter> LaunchParameters
        {
            get
            {
                GatherLaunchParameters();
                return m_LaunchablesLaunchParameters;
            }
        }

        List<LaunchParameterValue> LaunchParametersValue { get; set; } = new();

        RadzenDataGrid<LaunchComplex> m_ComplexesGrid = default!;
        RadzenDropDownDataGrid<Guid>? m_AssetsDropDown;

        void GatherLaunchParameters()
        {
            HashSet<string> launchableNames = new();
            if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                foreach (var complex in Complexes.Collection.Values)
                {
                    foreach (var launchPad in complex.LaunchPads)
                    {
                        var configuration = GetConfigurationForLaunchPad(complex, launchPad);
                        var launchable = configuration.GetEffectiveLaunchable(asset, launchPad);
                        if (launchable != null)
                        {
                            launchableNames.Add(launchable.Name);
                        }
                    }
                }

                var sortedLaunchableNames = launchableNames.OrderBy(n => n).ToList();
                if (m_CurrentLaunchables.assetId != asset.Id
                    || !m_CurrentLaunchables.launchableNames.SequenceEqual(sortedLaunchableNames))
                {
                    List<LaunchCatalog.LaunchParameter> launchableParameters = new();
                    foreach (var name in sortedLaunchableNames)
                    {
                        var launchable = asset.Launchables.FirstOrDefault(l => l.Name == name);
                        if (launchable != null)
                        {
                            launchableParameters.AddRange(launchable.GlobalParameters);
                        }
                    }
                    m_LaunchablesLaunchParameters = launchableParameters;
                    m_CurrentLaunchables = (asset.Id, sortedLaunchableNames);
                }
            }
            else
            {
                m_LaunchablesLaunchParameters = Enumerable.Empty<LaunchCatalog.LaunchParameter>();
                m_CurrentLaunchables = (Guid.Empty, Enumerable.Empty<string>());
            }
        }

        int GetActiveLaunchPads(Guid launchComplexId)
        {
            if (!AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                return 0;
            }

            if (!Complexes.Collection.TryGetValue(launchComplexId, out var complexDefinition))
            {
                return 0;
            }
            var complexConfiguration = LaunchConfigurationService.WorkValue.LaunchComplexes
                    .FirstOrDefault(lc => lc.Identifier == launchComplexId);
            if (complexConfiguration == null)
            {
                return 0;
            }

            int activeCount = 0;
            foreach (var launchPadConfiguration in complexConfiguration.LaunchPads.ToList())
            {
                var launchPad =
                    complexDefinition.LaunchPads.FirstOrDefault(c => c.Identifier == launchPadConfiguration.Identifier);
                if (launchPad != null)
                {
                    var launchable = launchPadConfiguration.GetEffectiveLaunchable(asset, launchPad);
                    if (launchable != null)
                    {
                        ++activeCount;
                    }
                    else
                    {
                        launchPadConfiguration.LaunchableName = "";
                    }
                }
            }

            return activeCount;
        }

        async Task ConfigureLaunchComplex(LaunchComplex complex)
        {

            var toEdit =
                LaunchConfigurationService.WorkValue.LaunchComplexes.FirstOrDefault(c => c.Identifier == complex.Id);
            if (toEdit == null)
            {
                toEdit = new() { Identifier = complex.Id };
                LaunchConfigurationService.WorkValue.LaunchComplexes =
                    LaunchConfigurationService.WorkValue.LaunchComplexes.Append(toEdit).ToList();
                LaunchConfigurationService.WorkValue.SignalChanges();
                await ApplyChangesAsync();
            }

            if (!AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                return;
            }

            var ret = await DialogService.OpenAsync<Dialogs.EditLaunchComplexConfiguration>($"Configure {complex.Name}",
                new Dictionary<string, object> { { "Asset", asset }, { "Complex", complex }, { "ToEdit", toEdit } },
                new DialogOptions() { Width = "60%", Height = "auto", Resizable = true, Draggable = true });
            if (!(bool)(ret ?? false))
            {
                return;
            }

            LaunchConfigurationService.WorkValue.SignalChanges();
            await ApplyChangesAsync();
        }

        async Task Launch()
        {
            var hasAsset = LaunchConfigurationService.WorkValue.AssetId != Guid.Empty;
            if (!hasAsset)
            {
                return;
            }

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                if (!await ApplyChangesAsync())
                {
                    return;
                }
            }

            await MissionCommandsService.LaunchMissionAsync();
        }

        Task Stop()
        {
            return MissionCommandsService.StopCurrentMissionAsync();
        }

        async Task<bool> ApplyChangesAsync()
        {
            // Check we are not overwriting someone else's work.
            if (LaunchConfigurationService.HasMissionControlValueChangedSinceWorkValueModified)
            {
                var ret = await DialogService.CustomConfirm($"Launch configuration has be modified by an external source " +
                    $"since you started working on it.  Do you want to overwrite those changes?",
                    "Overwrite confirmation", new() { OkButtonText = "Yes", CancelButtonText = "No" });
                if (!ret)
                {
                    return false;
                }
            }

            // Let's do some cleanup before applying changes
            if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
            {
                foreach (var complexConfiguration in LaunchConfigurationService.WorkValue.LaunchComplexes.ToList())
                {
                    if (Complexes.Collection.TryGetValue(complexConfiguration.Identifier, out var complex))
                    {
                        foreach (var launchPadConfiguration in complexConfiguration.LaunchPads.ToList())
                        {
                            var launchPad =
                                complex.LaunchPads.FirstOrDefault(c => c.Identifier == launchPadConfiguration.Identifier);
                            if (launchPad != null)
                            {
                                var launchable = launchPadConfiguration.GetEffectiveLaunchable(asset, launchPad);
                                launchPadConfiguration.LaunchableName = launchable != null ? launchable.Name : "";
                            }
                            else
                            {
                                complexConfiguration.LaunchPads = complexConfiguration.LaunchPads
                                    .Where(l => l.Identifier != launchPadConfiguration.Identifier).ToList();
                            }
                        }
                    }

                    if (complex == null || !complexConfiguration.LaunchPads.Any())
                    {
                        LaunchConfigurationService.WorkValue.LaunchComplexes =
                            LaunchConfigurationService.WorkValue.LaunchComplexes
                                .Where(c => c.Identifier != complexConfiguration.Identifier).ToList();
                    }
                }
            }

            // Send to Mission Control
            await LaunchConfigurationService.PushWorkToMissionControlAsync();

            // Done
            return true;
        }

        async Task ResetToDefault()
        {
            var ret = await DialogService.CustomConfirm("Do you want to reset all parameters to their default values? This cannot be undone.",
                "Reset all to default", new() { OkButtonText = "Reset all" });

            if (!ret) return;

            LaunchConfigurationService.ResetParametersToDefault();
            await ApplyChangesAsync();
        }

        async Task LaunchParametersValueUpdate(List<LaunchParameterValue> updatedValues)
        {
            Debug.Assert(ReferenceEquals(updatedValues, LaunchParametersValue));
            LaunchConfigurationService.WorkValue.SignalChanges();
            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                await ApplyChangesAsync();
            }
        }

        void MissionControlStatusChanged(ObservableObject obj)
        {
            StateHasChanged();
        }

        void AssetsChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_AssetsDropDown?.Reload();
        }

        async Task OnSelectedAssetChanged()
        {
            await ApplyChangesAsync();
        }

        void LaunchConfigurationChanged(ObservableObject obj)
        {
            if (!LaunchConfigurationService.WorkValueNeedsPush)
            {
                // Work configuration is still follow mission control, so update out launch parameters values (that
                // will be modified by Components.LaunchParameters).
                SyncParameterValuesToWorkLaunchConfiguration();
            }
            StateHasChanged();
        }

        void ComplexesChanged(IReadOnlyIncrementalCollection obj)
        {
            StateHasChanged();
            m_ComplexesGrid.Reload();
        }

        void SyncParameterValuesToWorkLaunchConfiguration()
        {
            if (!ReferenceEquals(LaunchParametersValue, LaunchConfigurationService.WorkValue.Parameters))
            {
                LaunchParametersValue.Clear();
                LaunchParametersValue.AddRange(LaunchConfigurationService.WorkValue.Parameters);
                Debug.Assert(LaunchParametersValue.SequenceEqual(LaunchConfigurationService.WorkValue.Parameters));
                LaunchConfigurationService.WorkValue.Parameters = LaunchParametersValue;
            }
        }

        /// <summary>
        /// Returns the <see cref="LaunchPadConfiguration"/> for the requested <see cref="MissionControl.LaunchPad"/>.
        /// </summary>
        /// <param name="launchComplex"><see cref="LaunchComplex"/> the <see cref="MissionControl.LaunchPad"/> is a
        /// part of.</param>
        /// <param name="launchPad">The <see cref="MissionControl.LaunchPad"/>.</param>
        LaunchPadConfiguration GetConfigurationForLaunchPad(LaunchComplex launchComplex, MissionControl.LaunchPad launchPad)
        {
            var complexCfg = LaunchConfigurationService.WorkValue.LaunchComplexes
                .FirstOrDefault(lc => lc.Identifier == launchComplex.Id);
            if (complexCfg == null)
            {
                complexCfg = new() { Identifier = launchComplex.Id };
                LaunchConfigurationService.WorkValue.LaunchComplexes =
                    LaunchConfigurationService.WorkValue.LaunchComplexes.Append(complexCfg).ToList();
            }

            var padCfg = complexCfg.LaunchPads.FirstOrDefault(c => c.Identifier == launchPad.Identifier);
            if (padCfg == null)
            {
                padCfg = new() { Identifier = launchPad.Identifier };
                if (AssetsService.Collection.TryGetValue(LaunchConfigurationService.WorkValue.AssetId, out var asset))
                {
                    padCfg.LaunchableName = launchPad.GetCompatibleLaunchables(asset).Select(l => l.Name).FirstOrDefault("");
                }
                complexCfg.LaunchPads = complexCfg.LaunchPads.Append(padCfg).ToList();
            }

            return padCfg;
        }

        async Task OnSaveClick(RadzenSplitButtonItem item)
        {
            await SaveAs(overwrite: item != null);
        }

        async Task SaveAs(bool overwrite = false)
        {
            var title = overwrite ? "Overwrite Snapshot" : "New Snapshot";
            var dialogParams = new Dictionary<string, object>();
            if (overwrite)
            {
                dialogParams["Overwritables"] = MissionsService.Collection.Values;
            }
            var ret = await DialogService.OpenAsync<SaveDialog>(title,
                dialogParams,
                new DialogOptions() { Width = "80%", Height = "60%",
                    Resizable = true,
                    Draggable = true
                });

            if (ret == null)
            {
                return;
            }

            if (LaunchConfigurationService.WorkValueNeedsPush)
            {
                await LaunchConfigurationService.PushWorkToMissionControlAsync();
            }

            var command = (SaveMissionCommand)ret;
            await MissionCommandsService.SaveMissionAsync(command);
            NotificationService.Notify(NotificationSeverity.Info, $"Configuration saved as \"{command.Description.Name}\"");
        }

    }
}
