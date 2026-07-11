// NorthernLink.Modules.Clients.Contracts
//
// Public surface of the Clients module: integration events and cross-module DTOs only.
// Keep types here small, serializable, and stable — every other module is allowed to
// depend on them. Integration event names should end in "IntegrationEvent" so the
// routing key convention (clients.<event-name>) derives cleanly.
