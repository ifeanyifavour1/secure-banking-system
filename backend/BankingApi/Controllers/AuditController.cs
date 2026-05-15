// GET /api/audit
//   - [AdminOnly] Query the audit_log table
//   - Filter by: entity_type, entity_id, action, performed_by, date range
//   - Paginated results ordered by created_at DESC
//   - Used for compliance review and incident investigation
