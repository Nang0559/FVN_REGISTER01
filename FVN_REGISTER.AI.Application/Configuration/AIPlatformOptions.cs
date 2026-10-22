namespace FVN_REGISTER.AI.Application.Configuration;
public sealed class AIPlatformOptions
{
 public const string SectionName="AIPlatform";
 public string DeploymentMode{get;set;}="Hybrid";
 public string DefaultProvider{get;set;}="Unconfigured";
 public string? Endpoint{get;set;}
 public string? Model{get;set;}
 public string? EmbeddingModel{get;set;}
 public int MaximumFileSizeMb{get;set;}=25;
 public int MaximumConcurrentJobs{get;set;}=2;
 public bool RequireHumanApprovalBeforeBusinessWrite{get;set;}=true;
 public bool EnableExternalProvider{get;set;}
 public string StorageRoot{get;set;}="App_Data/FvnAi";
 public string ConnectionStringName{get;set;}="FvnAi";
 public string? ApiKey{get;set;}
 public string? ContextSigningKey{get;set;}
 public bool RequireContentBoundSignatures{get;set;}=true;
 public int QueuePollSeconds{get;set;}=15;
 /// <summary>Upper bound for one local-LLM (Ollama) chat call. A CPU-only 3B model can need well over a minute.</summary>
 public int ModelTimeoutSeconds{get;set;}=180;
 public int RetentionPollMinutes{get;set;}=30;
 public int ConversationMaintenancePollMinutes{get;set;}=5;
 public int ConnectorPollMinutes{get;set;}=5;
 public List<FolderConnectorOptions> FolderConnectors{get;set;}=[];
}
