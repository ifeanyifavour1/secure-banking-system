// AuditLogEntry entity — maps to the 'audit_log' table
//
// Fields:
//   log_id (BIGINT PK), event_type, entity_type, entity_id,
//   action (create|read|update|delete|login|logout|failed_login),
//   performed_by (FK → users, nullable for system events),
//   old_values (JSONB), new_values (JSONB),
//   ip_address (INET), user_agent, additional_info (JSONB),
//   created_at
