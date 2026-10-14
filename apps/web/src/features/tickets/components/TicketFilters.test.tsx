import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../hooks/useTicketList';
import { TicketFilters } from './TicketFilters';

describe('TicketFilters SLA status', () => {
  it('provides every backend SLA value and reports selection and clearing', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    render(
      <TicketFilters
        filters={emptyFilters}
        agents={[]}
        categories={[]}
        customers={[]}
        onChange={onChange}
        onReset={vi.fn()}
      />,
    );

    const select = screen.getByRole('combobox', { name: 'SLA status' });
    expect(select).toHaveValue('');
    expect(screen.getByRole('option', { name: 'All SLA statuses' })).toHaveValue('');
    expect(screen.getByRole('option', { name: 'Within SLA' })).toHaveValue('WithinSla');
    expect(screen.getByRole('option', { name: 'At risk' })).toHaveValue('AtRisk');
    expect(screen.getByRole('option', { name: 'Breached' })).toHaveValue('Breached');
    expect(screen.getByRole('option', { name: 'Met' })).toHaveValue('Met');
    expect(screen.getByRole('option', { name: 'No SLA' })).toHaveValue('NotApplicable');

    await user.selectOptions(select, 'AtRisk');
    expect(onChange).toHaveBeenLastCalledWith({ slaStatus: 'AtRisk' });

    await user.selectOptions(select, '');
    expect(onChange).toHaveBeenLastCalledWith({ slaStatus: undefined });
  });
});
