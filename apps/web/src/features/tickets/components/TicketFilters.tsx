import type {
  Agent,
  Category,
  CustomerListItem,
  SlaStatus,
  TicketFilters as Filters,
  TicketPriority,
  TicketStatus,
} from '../../../types/api';
import { humanize } from '../../../lib/format';

const statuses: TicketStatus[] = ['New', 'Open', 'InProgress', 'Resolved', 'Closed'];
const priorities: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];
const slaStatuses: ReadonlyArray<{ value: SlaStatus; label: string }> = [
  { value: 'WithinSla', label: 'Within SLA' },
  { value: 'AtRisk', label: 'At risk' },
  { value: 'Breached', label: 'Breached' },
  { value: 'Met', label: 'Met' },
  { value: 'NotApplicable', label: 'No SLA' },
];
const sortOptions: ReadonlyArray<{
  value: string;
  label: string;
  sortBy: Filters['sortBy'];
  sortDirection: Filters['sortDirection'];
}> = [
  { value: 'createdAtUtc:desc', label: 'Newest first', sortBy: 'createdAtUtc', sortDirection: 'desc' },
  { value: 'createdAtUtc:asc', label: 'Oldest first', sortBy: 'createdAtUtc', sortDirection: 'asc' },
  { value: 'dueAtUtc:asc', label: 'Due soonest', sortBy: 'dueAtUtc', sortDirection: 'asc' },
  { value: 'priority:desc', label: 'Highest priority', sortBy: 'priority', sortDirection: 'desc' },
  { value: 'updatedAtUtc:desc', label: 'Recently updated', sortBy: 'updatedAtUtc', sortDirection: 'desc' },
];

interface TicketFiltersProps {
  filters: Filters;
  agents: Agent[];
  categories: Category[];
  customers: CustomerListItem[];
  onChange: (patch: Partial<Filters>) => void;
  onReset: () => void;
}

/** The filter bar above the ticket list. Presentational: it only reports changes upwards. */
export function TicketFilters({
  filters,
  agents,
  categories,
  customers,
  onChange,
  onReset,
}: TicketFiltersProps) {
  const toNumber = (value: string) => (value === '' ? undefined : Number(value));

  return (
    <section className="filters" aria-label="Ticket filters">
      <div className="filters__row">
        <div className="field">
          <label className="field__label" htmlFor="filter-search">
            Search
          </label>
          <input
            id="filter-search"
            type="search"
            placeholder="Title, reference or customer"
            value={filters.search}
            onChange={(event) => onChange({ search: event.target.value })}
          />
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-status">
            Status
          </label>
          <select
            id="filter-status"
            value={filters.status ?? ''}
            onChange={(event) => onChange({ status: findValue(statuses, event.target.value) })}
          >
            <option value="">All statuses</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {humanize(status)}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-priority">
            Priority
          </label>
          <select
            id="filter-priority"
            value={filters.priority ?? ''}
            onChange={(event) => onChange({ priority: findValue(priorities, event.target.value) })}
          >
            <option value="">All priorities</option>
            {priorities.map((priority) => (
              <option key={priority} value={priority}>
                {priority}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-sla-status">
            SLA status
          </label>
          <select
            id="filter-sla-status"
            value={filters.slaStatus ?? ''}
            onChange={(event) =>
              onChange({
                slaStatus: slaStatuses.find(({ value }) => value === event.target.value)?.value,
              })
            }
          >
            <option value="">All SLA statuses</option>
            {slaStatuses.map(({ value, label }) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="filters__row">
        <div className="field">
          <label className="field__label" htmlFor="filter-category">
            Category
          </label>
          <select
            id="filter-category"
            value={filters.categoryId ?? ''}
            onChange={(event) => onChange({ categoryId: toNumber(event.target.value) })}
          >
            <option value="">All categories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-customer">
            Customer
          </label>
          <select
            id="filter-customer"
            value={filters.customerId ?? ''}
            onChange={(event) => onChange({ customerId: toNumber(event.target.value) })}
          >
            <option value="">All customers</option>
            {customers.map((customer) => (
              <option key={customer.id} value={customer.id}>
                {customer.name}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-agent">
            Assigned agent
          </label>
          <select
            id="filter-agent"
            value={filters.assignedAgentId ?? ''}
            onChange={(event) => onChange({ assignedAgentId: toNumber(event.target.value) })}
          >
            <option value="">Any agent</option>
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.fullName}
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label className="field__label" htmlFor="filter-sort">
            Sort by
          </label>
          <select
            id="filter-sort"
            value={`${filters.sortBy}:${filters.sortDirection}`}
            onChange={(event) => {
              const option = sortOptions.find(({ value }) => value === event.target.value);
              if (option) {
                onChange({ sortBy: option.sortBy, sortDirection: option.sortDirection });
              }
            }}
          >
            {sortOptions.map(({ value, label }) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="filters__row filters__row--actions">
        <label className="checkbox">
          <input
            type="checkbox"
            checked={filters.unassignedOnly ?? false}
            onChange={(event) => onChange({ unassignedOnly: event.target.checked })}
          />
          Unassigned only
        </label>

        <button type="button" className="button button--ghost" onClick={onReset}>
          Clear filters
        </button>
      </div>
    </section>
  );
}

function findValue<T extends string>(allowed: readonly T[], value: string): T | undefined {
  return allowed.find((candidate) => candidate === value);
}
