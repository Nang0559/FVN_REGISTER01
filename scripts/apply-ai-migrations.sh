#!/usr/bin/env bash
set -euo pipefail

SQLCMD_PATH="${SQLCMD_PATH:-sqlcmd}"
SQLCMD_SERVER="${SQLCMD_SERVER:-localhost}"
SQLCMD_DATABASE="${SQLCMD_DATABASE:-FVN_AI}"
SQLCMD_USER="${SQLCMD_USER:-sa}"
SQLCMD_PASSWORD="${SQLCMD_PASSWORD:?Set SQLCMD_PASSWORD}"
MIGRATIONS_DIR="${MIGRATIONS_DIR:-SQL/AI}"
args=(-S "$SQLCMD_SERVER" -d "$SQLCMD_DATABASE" -U "$SQLCMD_USER" -P "$SQLCMD_PASSWORD" -C -b)

migrations=(
  001_CreateAiIngestion.sql
  002_ReliableIngestionQueue.sql
  003_Review_Knowledge_Feedback.sql
  004_HR_Reconciliation_Cases.sql
  005_Universal_AI_Core.sql
  006_Document_Schema_Routing.sql
  007_UniversalDocumentLifecycle.sql
  008_Phase1_3_Retention_Representation.sql
  009_Phase4_5_Knowledge_ActiveLearning.sql
  010_Extraction_Deduplication_And_MigrationLedger.sql
  011_Persistent_Request_Replay_Protection.sql
  012_KnowledgeChunkConstraint_And_ChecksumLedger.sql
  013_HRCaseTextColumnSizeRepair.sql
  014_Phase5_ReleaseRetentionTenantHardening.sql
  015_DocumentDataEntry.sql
  016_ConversationPersistence.sql
  017_DeadLetterDocumentStatus.sql
  018_ConversationPendingRecovery.sql
  019_DeadLetterProcedureAndTriggerCompatibility.sql
  020_AtomicSerializedQueueClaims.sql
  021_FailProcedureAnsiQuotedSettings.sql
  022_DropLegacyDocumentFingerprintIndex.sql
)

scalar() {
  "$SQLCMD_PATH" "${args[@]}" -h -1 -W -Q "SET NOCOUNT ON; $1" | tr -d '\r' | sed '/^[[:space:]]*$/d' | tail -n 1 | xargs
}

fresh=0
exists="$(scalar "SELECT CASE WHEN OBJECT_ID(N'ai.AiDocuments',N'U') IS NULL THEN 0 ELSE 1 END")"
if [[ "$exists" == "0" ]]; then fresh=1; fi

for file in "${migrations[@]}"; do
  path="$MIGRATIONS_DIR/$file"
  [[ -f "$path" ]] || { echo "Missing migration: $path" >&2; exit 2; }
  id="${file%.sql}"
  checksum="$(sha256sum "$path" | awk '{print toupper($1)}')"
  has_ledger="$(scalar "SELECT CASE WHEN OBJECT_ID(N'ai.AiSchemaMigrations',N'U') IS NOT NULL AND COL_LENGTH(N'ai.AiSchemaMigrations',N'Sha256Checksum') IS NOT NULL THEN 1 ELSE 0 END")"
  if [[ "$has_ledger" == "1" ]]; then
    recorded="$(scalar "SELECT COALESCE(MAX(Sha256Checksum),'') FROM ai.AiSchemaMigrations WHERE MigrationId=N'$id'")"
    if [[ -n "$recorded" && "$recorded" != "$checksum" ]]; then
      echo "Checksum mismatch for already-recorded migration $file; do not edit applied migrations. Create a new migration." >&2
      exit 3
    fi
  fi
  echo "Applying $file"
  "$SQLCMD_PATH" "${args[@]}" -i "$path"
done

for file in "${migrations[@]}"; do
  id="${file%.sql}"
  checksum="$(sha256sum "$MIGRATIONS_DIR/$file" | awk '{print toupper($1)}')"
  num="${id:0:3}"
  if [[ "$fresh" == "1" || "$num" == "010" || "$num" == "011" || "$num" == "012" || "$num" == "013" || "$num" == "014" || "$num" == "015" || "$num" == "016" || "$num" == "017" || "$num" == "018" || "$num" == "019" || "$num" == "020" || "$num" == "021" || "$num" == "022" ]]; then
    checksumSql="'$checksum'"
  else
    checksumSql="NULL"
  fi
  "$SQLCMD_PATH" "${args[@]}" -Q "
    IF EXISTS (SELECT 1 FROM ai.AiSchemaMigrations WHERE MigrationId=N'$id')
      UPDATE ai.AiSchemaMigrations
         SET MigrationFileName=N'$file', Sha256Checksum=COALESCE(Sha256Checksum,$checksumSql)
       WHERE MigrationId=N'$id';
    ELSE
      INSERT INTO ai.AiSchemaMigrations(MigrationId,MigrationFileName,Sha256Checksum)
      VALUES(N'$id',N'$file',$checksumSql);
  "
done

echo "Applied all 22 AI migrations; checksum ledger updated."
