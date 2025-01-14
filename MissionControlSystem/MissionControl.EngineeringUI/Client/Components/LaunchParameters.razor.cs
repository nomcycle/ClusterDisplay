using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.LaunchCatalog;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Components
{
    public partial class LaunchParameters
    {
        [Parameter]
        public IEnumerable<LaunchParameter> Parameters { get; set; } = Enumerable.Empty<LaunchParameter>();

        /// <summary>
        /// The parameter values that we are ultimately editing.
        /// </summary>
        [Parameter]
        public List<LaunchParameterValue> Values { get; set; } = new();
        [Parameter]
        public string NestedPrefix { get; set; } = "";

        [Parameter]
        public EventCallback<List<LaunchParameterValue>> OnValuesUpdated { get; set; }

        [Inject]
        MissionControlStatusService MissionControlStatus { get; set; } = default!;
        [Inject]
        DialogService DialogService { get; set; } = default!;
        [Inject]
        ILogger<LaunchParameters> Logger { get; set; } = default!;

        /// <summary>
        /// One entry displayed in the grid
        /// </summary>
        public abstract class Entry
        {
            public abstract string Name { get; }
            public abstract object Value { get; }
            public abstract bool IsEmpty { get; }
            public abstract void Clear();
        }

        /// <summary>
        /// One entry representing a parameter in the grid
        /// </summary>
        public class ParameterEntry : Entry
        {
            public LaunchParameter? Definition { get; set; }
            public LaunchParameterValue? ParameterValue { get; set; }
            public string Id { get; init; } = "";

            public string? ValidationError { get; set; } = null;

            public override string Name => Definition?.Name ?? $"(Unused: {ParameterValue?.Id ?? ""})";
            public override object Value => ParameterValue?.Value ?? (Definition?.DefaultValue ?? "unknown");
            public override bool IsEmpty => Definition == null && ParameterValue == null;
            public override void Clear()
            {
                Definition = null;
                ParameterValue = null;
                ValidationError = null;
            }
        }

        /// <summary>
        /// An entry that consists of a group of parameters.
        /// </summary>
        /// <remarks>
        /// Each <see cref="NestedEntry"/> generates a recursive <see cref="LaunchParameter"/> component
        /// in the UI.
        /// </remarks>
        protected class NestedEntry : Entry
        {
            public string NestedPrefix { get; set; } = "";
            public string NestedName { get; init; } = "";
            public List<LaunchParameter> Parameters { get; set; } = new();
            public List<LaunchParameterValue> Values { get; set; } = new();
            public List<LaunchParameterValue> PreviousValues { get; set; } = new();

            public override string Name => NestedName;
            public override object Value => "";
            public override bool IsEmpty => !Parameters.Any() && !Values.Any();
            public override void Clear()
            {
                Parameters.Clear();
                Values.Clear();
                PreviousValues.Clear();
            }
        }

        List<Entry> Entries { get; } = new();

        RadzenDataGrid<Entry>? m_EntriesGrid;

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            FillEntries();
            m_EntriesGrid?.Reload();
        }

        static void RowRender(RowRenderEventArgs<Entry> args)
        {
            args.Expandable = args.Data is NestedEntry;
        }

        void OnParameterValueChanged(ParameterEntry entry)
        {
            if (entry.ValidationError != null)
            {
                Logger.LogWarning($"{entry.Name}: {entry.ValidationError}");
                return;
            }

            Debounce(async () =>
            {
                int valueIndex = Values.FindIndex(0, v => v.Id == entry.Id);
                if (valueIndex >= 0)
                {
                    if (entry.ParameterValue is not null)
                    {
                        Values[valueIndex] = entry.ParameterValue;
                    }
                    else
                    {
                        Values.RemoveAt(valueIndex);
                    }
                }
                else if (entry.ParameterValue is not null)
                {
                    Values.Add(entry.ParameterValue);
                }
                await OnValuesUpdated.InvokeAsync(Values);
            });
        }

        async Task OnNestedValuesChanged(List<LaunchParameterValue> nestedValues)
        {
            // Easy case, deal with changed and new values
            foreach (var nestedValue in nestedValues)
            {
                int previousValueIndex = Values.FindIndex(v => v.Id == nestedValue.Id);
                if (previousValueIndex >= 0)
                {
                    Values[previousValueIndex] = nestedValue;
                }
                else
                {
                    Values.Add(nestedValue);
                }
            }

            // A little bit more tricky, discover removed ones
            var nestedEntry = (NestedEntry?)Entries.FirstOrDefault(
                e => e is NestedEntry nestedEntry && ReferenceEquals(nestedValues, nestedEntry.Values));
            if (nestedEntry == null)
            {
                Logger.LogError("Can't find NestedEntry matching List<LaunchParameterValue>");
                return;
            }
            foreach (var prevValue in nestedEntry.PreviousValues.ToArray())
            {
                if (nestedValues.All(v => v.Id != prevValue.Id))
                {
                    int toRemoveIndex = Values.FindIndex(v => v.Id == prevValue.Id);
                    if (toRemoveIndex >= 0)
                    {
                        Values.RemoveAt(toRemoveIndex);
                    }
                }
            }

            // And last inform anyone listening for our changes
            await OnValuesUpdated.InvokeAsync(Values);
        }

        void FillEntries()
        {
            // Clear current entries' content (not the list of entries, the content of the entries, otherwise the grid
            // would perform too many updates for nothing, collapsing things around, loosing selection, ...).
            foreach (var entry in Entries)
            {
                entry.Clear();
            }

            // Dispatch parameters definition
            Dictionary<string, Entry> parametersEntry = new();
            foreach (var parameter in Parameters)
            {
                if (parameter.Hidden)
                {
                    continue;
                }
                string remainingGroup = parameter.Group;
                if (NestedPrefix != "")
                {
                    if (!remainingGroup.StartsWith(NestedPrefix))
                    {
                        Logger.LogWarning("{ParamId} group is {Group} while current nested prefix is {Current}, will " +
                            "be skipped", parameter.Id, remainingGroup, NestedPrefix);
                        continue;
                    }
                    remainingGroup = remainingGroup.Substring(NestedPrefix.Length);
                    if (remainingGroup.StartsWith('/'))
                    {
                        remainingGroup = remainingGroup.Substring(1);
                    }
                }
                if (remainingGroup == "")
                {
                    // Can we recycle an old entry
                    var parameterEntry = (ParameterEntry?)Entries
                        .FirstOrDefault(e => e is ParameterEntry parameterEntry && parameterEntry.Id == parameter.Id);
                    if (parameterEntry != null)
                    {
                        parameterEntry.Definition = parameter;
                    }
                    else
                    {
                        parameterEntry = new() { Id = parameter.Id, Definition = parameter };
                        Entries.Add(parameterEntry);
                    }
                    parametersEntry[parameter.Id] = parameterEntry;
                }
                else
                {
                    string nestedName = remainingGroup.Split('/').First();
                    var nestedEntry = (NestedEntry?)Entries.FirstOrDefault(
                        e => e is NestedEntry nestedEntry && nestedEntry.NestedName == nestedName);
                    if (nestedEntry == null)
                    {
                        nestedEntry = new NestedEntry()
                        {
                            NestedPrefix = $"{NestedPrefix}/{nestedName}",
                            NestedName = nestedName
                        };
                        if (nestedEntry.NestedPrefix.StartsWith('/'))
                        {
                            nestedEntry.NestedPrefix = nestedEntry.NestedPrefix.Substring(1);
                        }
                        Entries.Add(nestedEntry);
                    }
                    nestedEntry.Parameters.Add(parameter);
                    parametersEntry[parameter.Id] = nestedEntry;
                }
            }

            // Dispatch values
            foreach (var value in Values)
            {
                if (parametersEntry.TryGetValue(value.Id, out var entry))
                {
                    if (entry is ParameterEntry parameterEntry)
                    {
                        parameterEntry.ParameterValue = value;
                    }
                    else if (entry is NestedEntry nestedEntry)
                    {
                        nestedEntry.Values.Add(value);
                    }
                }
                else
                {
                    Entries.Add(new ParameterEntry { ParameterValue = value });
                }
            }

            // Clear entries that aren't used anymore
            foreach (var entry in Entries.ToArray())
            {
                if (entry.IsEmpty)
                {
                    Entries.Remove(entry);
                }
            }

            // Build the final list of values for nested entries
            foreach (var entry in Entries)
            {
                if (entry is NestedEntry nestedEntry)
                {
                    nestedEntry.PreviousValues.Clear();
                    nestedEntry.PreviousValues.AddRange(nestedEntry.Values);
                }
            }
        }

        // Is the field currently showing the default value for the parameter?
        static bool ShowingDefaultValue(Entry entry) =>
            entry switch
            {
                ParameterEntry parameterEntry => parameterEntry.ParameterValue is null && parameterEntry.ValidationError is null,
                NestedEntry nestedEntry => !nestedEntry.Values.Any(),
                _ => throw new ArgumentOutOfRangeException(nameof(entry))
            };

        async Task ResetToDefault(Entry entry)
        {
            switch (entry)
            {
                case ParameterEntry parameterEntry:
                    parameterEntry.ParameterValue = null;
                    parameterEntry.ValidationError = null;
                    OnParameterValueChanged(parameterEntry);
                    break;
                case NestedEntry nestedEntry:
                    nestedEntry.Values.Clear();
                    await OnNestedValuesChanged(nestedEntry.Values);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry));
            }
        }
    }
}
