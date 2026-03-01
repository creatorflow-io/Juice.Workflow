using Juice.Domain;
using Newtonsoft.Json;

namespace Juice.Workflows.Domain.AggregatesModel.DefinitionAggregate
{
    public class WorkflowDefinition : AuditAggregrateRoot<string, INotification>
    {
        public WorkflowDefinition(string id, string name):base(id, name)
        {
            Id = id;
            Name = name;
        }
        /// <summary>
        /// Raw data of workflow definition in yml, json or xml format
        /// </summary>
        public string? RawData { get; private set; }
        /// <summary>
        /// Describe raw data format to deserialize to executable data
        /// </summary>
        public string? RawFormat { get; private set; }
        /// <summary>
        /// Parsed data that ready to execute
        /// </summary>
        public string? Data { get; private set; }
        /// <summary>
        /// Lifecycle status: Draft (default) → Active (via Publish) → Archived
        /// </summary>
        public WorkflowDefinitionStatus Status { get; private set; } = WorkflowDefinitionStatus.Draft;

        /// <summary>
        /// Promotes Draft → Active. Throws if status is not Draft or if Data has not been set.
        /// </summary>
        public void Publish()
        {
            if (Status != WorkflowDefinitionStatus.Draft)
            {
                throw new InvalidOperationException($"Only Draft definitions can be published. Current status: {Status}.");
            }
            if (string.IsNullOrEmpty(Data))
            {
                throw new InvalidOperationException("Definition has no execution data. Save the definition before publishing.");
            }
            Status = WorkflowDefinitionStatus.Active;
        }

        /// <summary>
        /// Retires Active → Archived. Throws if status is not Active.
        /// </summary>
        public void Archive()
        {
            if (Status != WorkflowDefinitionStatus.Active)
            {
                throw new InvalidOperationException($"Only Active definitions can be archived. Current status: {Status}.");
            }
            Status = WorkflowDefinitionStatus.Archived;
        }

        /// <summary>
        /// Updates the display name.
        /// </summary>
        public void Rename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Name cannot be empty.", nameof(newName));
            Name = newName;
        }

        public void UpdateRawData(string rawData, string rawFormat)
        {
            RawData = rawData;
            RawFormat = rawFormat;
        }

        public void ClearData()
        {
            Data = default;
            this.AddDomainEvent(new DefinitionDataChangedDomainEvent(this, Array.Empty<ProcessRecord>(), Array.Empty<NodeData>(), Array.Empty<FlowData>()));
        }

        public void SetData(IEnumerable<ProcessRecord> processes, IEnumerable<NodeData> nodes, IEnumerable<FlowData> flows)
        {
            Data = JsonConvert.SerializeObject((processes, nodes, flows));
            this.AddDomainEvent(new DefinitionDataChangedDomainEvent(this, processes, nodes, flows));
        }

        public (IEnumerable<ProcessRecord> Processes, IEnumerable<NodeData> Nodes, IEnumerable<FlowData> Flows) GetData()
        {
            return JsonConvert.DeserializeObject<(IEnumerable<ProcessRecord>, IEnumerable<NodeData>, IEnumerable<FlowData>)>(Data ?? "{}");
        }
    }
}
