import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import Page from './+page.svelte';

const listResponse = (
	items: unknown[] = [],
	sources: Record<string, { status: string }> = { local: { status: 'ok' } }
) => ({
	items,
	sources
});

const googleListResponse = (status: string) => listResponse([], {
	local: { status: 'ok' },
	google: { status }
});

const reservation = (overrides: Record<string, unknown> = {}) => ({
	id: 'local:1',
	source: 'local',
	dogName: 'Burek',
	startDate: '2026-05-10',
	endDate: '2026-05-12',
	createdAt: '2026-05-02T10:00:00Z',
	canDelete: true,
	...overrides
});

beforeEach(() => {
	vi.restoreAllMocks();
});

describe('Reservation form', () => {
	it('disables submit button when any field is empty', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse())
		}));

		render(Page);
		const button = screen.getByRole('button', { name: /dodaj/i });
		expect(button).toBeDisabled();
	});

	it('shows "Dodawanie..." and disables button during submission', async () => {
		const user = userEvent.setup();
		let resolvePost: (value: Response) => void;
		const postPromise = new Promise<Response>((r) => { resolvePost = r; });

		vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, opts?: RequestInit) => {
			if (opts?.method === 'POST') return postPromise;
			return Promise.resolve({ ok: true, json: () => Promise.resolve(listResponse()) });
		}));

		render(Page);

		const dogName = screen.getByLabelText(/imię psa/i);
		const startDate = screen.getByLabelText(/od/i);
		const endDate = screen.getByLabelText(/do/i);

		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];

		await user.type(dogName, 'Burek');
		await user.type(startDate, tomorrow);
		await user.type(endDate, dayAfter);
		await user.click(screen.getByRole('button', { name: /dodaj/i }));

		const button = screen.getByRole('button');
		expect(button).toHaveTextContent(/dodawanie/i);
		expect(button).toBeDisabled();

		resolvePost!(new Response(JSON.stringify(reservation({ startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() })), {
			status: 201,
			headers: { 'Content-Type': 'application/json' }
		}));
	});

	it('displays server validation errors inline', async () => {
		const user = userEvent.setup();

		vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, opts?: RequestInit) => {
			if (opts?.method === 'POST') {
				return Promise.resolve({
					ok: false,
					status: 400,
					json: () => Promise.resolve({ errors: { dogName: 'Imię psa jest wymagane.' } })
				});
			}
			return Promise.resolve({ ok: true, json: () => Promise.resolve(listResponse()) });
		}));

		render(Page);

		const dogName = screen.getByLabelText(/imię psa/i);
		const startDate = screen.getByLabelText(/od/i);
		const endDate = screen.getByLabelText(/do/i);

		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];

		await user.type(dogName, 'x');
		await user.type(startDate, tomorrow);
		await user.type(endDate, dayAfter);
		await user.click(screen.getByRole('button', { name: /dodaj/i }));

		await waitFor(() => {
			expect(screen.getByText('Imię psa jest wymagane.')).toBeInTheDocument();
		});
	});

	it('announces server validation errors in a polite live region', async () => {
		const user = userEvent.setup();

		vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, opts?: RequestInit) => {
			if (opts?.method === 'POST') {
				return Promise.resolve({
					ok: false,
					status: 400,
					json: () => Promise.resolve({ errors: { dogName: 'ImiÄ™ psa jest wymagane.' } })
				});
			}
			return Promise.resolve({ ok: true, json: () => Promise.resolve(listResponse()) });
		}));

		render(Page);

		const liveRegion = document.querySelector('[aria-live="polite"]');
		expect(liveRegion).toBeInTheDocument();
		expect(liveRegion).toBeEmptyDOMElement();

		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];

		await user.type(screen.getByLabelText(/psa/i), 'x');
		await user.type(screen.getByLabelText(/od/i), tomorrow);
		await user.type(screen.getByLabelText(/do/i), dayAfter);
		await user.click(screen.getByRole('button', { name: /dodaj/i }));

		await waitFor(() => {
			expect(liveRegion).toHaveTextContent('ImiÄ™ psa jest wymagane.');
		});
	});

	it('clears form on successful submission', async () => {
		const user = userEvent.setup();
		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];

		vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, opts?: RequestInit) => {
			if (opts?.method === 'POST') {
				return Promise.resolve({
					ok: true,
					status: 201,
					json: () => Promise.resolve(reservation({ startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() }))
				});
			}
			return Promise.resolve({ ok: true, json: () => Promise.resolve(listResponse()) });
		}));

		render(Page);

		const dogName = screen.getByLabelText(/imię psa/i) as HTMLInputElement;
		const startDate = screen.getByLabelText(/od/i) as HTMLInputElement;
		const endDate = screen.getByLabelText(/do/i) as HTMLInputElement;

		await user.type(dogName, 'Burek');
		await user.type(startDate, tomorrow);
		await user.type(endDate, dayAfter);
		await user.click(screen.getByRole('button', { name: /dodaj/i }));

		await waitFor(() => {
			expect(dogName).toHaveValue('');
			expect(startDate).toHaveValue('');
			expect(endDate).toHaveValue('');
		});
	});

	it('reloads the reservation list after successful submission', async () => {
		const user = userEvent.setup();
		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];
		const fetch = vi.fn()
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) })
			.mockResolvedValueOnce({
				ok: true,
				status: 201,
				json: () => Promise.resolve(reservation({ startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() }))
			})
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([
					reservation({ startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() })
				]))
			});
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.type(screen.getByLabelText(/imię psa/i), 'Burek');
		await user.type(screen.getByLabelText(/od/i), tomorrow);
		await user.type(screen.getByLabelText(/do/i), dayAfter);
		await user.click(screen.getByRole('button', { name: /dodaj/i }));

		expect(await screen.findByRole('row', { name: new RegExp(`Burek ${tomorrow} ${dayAfter}`, 'i') })).toBeInTheDocument();
		expect(fetch).toHaveBeenCalledTimes(3);
	});
});

describe('Reservation list', () => {
	it('shows loading state during initial fetch', () => {
		vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})));

		render(Page);

		expect(screen.getByText('Ładowanie...')).toBeInTheDocument();
	});

	it('shows empty state when there are no reservations', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse())
		}));

		render(Page);

		expect(await screen.findByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeInTheDocument();
	});

	it('shows fetch error and retries when requested', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({ ok: false })
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) });
		vi.stubGlobal('fetch', fetch);

		render(Page);

		expect(await screen.findByText('Nie udało się pobrać rezerwacji.')).toBeInTheDocument();
		await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }));

		await waitFor(() => {
			expect(fetch).toHaveBeenCalledTimes(2);
			expect(screen.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeInTheDocument();
		});
	});

	it('renders populated reservations', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse([reservation()]))
		}));

		render(Page);

		expect(await screen.findByRole('row', { name: /Burek 2026-05-10 2026-05-12/i })).toBeInTheDocument();
	});

	it('renders Google Calendar reservations returned by the aggregate API', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse([
				reservation({
					id: 'google:event-1',
					source: 'google',
					dogName: 'Figa',
					canDelete: false
				})
			], {
				local: { status: 'ok' },
				google: { status: 'ok' }
			}))
		}));

		render(Page);

		expect(await screen.findByRole('row', { name: /Figa 2026-05-10 2026-05-12 Google/i })).toBeInTheDocument();
		expect(screen.queryByRole('button', { name: 'Usuń rezerwację dla Figa' })).not.toBeInTheDocument();
	});

	it('opens a dog-specific delete confirmation dialog from a row action', async () => {
		const user = userEvent.setup();
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse([reservation()]))
		}));

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));

		const dialog = screen.getByRole('dialog', { name: /usunąć rezerwację/i });
		expect(dialog).toHaveTextContent('Burek');
		expect(screen.getByRole('button', { name: 'Anuluj' })).toBeInTheDocument();
		expect(screen.getByRole('button', { name: 'Usuń' })).toBeInTheDocument();
	});

	it('cancels deletion without calling the delete endpoint or changing the list', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse([reservation()]))
		});
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Anuluj' }));

		expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
		expect(screen.getByRole('row', { name: /Burek 2026-05-10 2026-05-12/i })).toBeInTheDocument();
		expect(fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE')).toHaveLength(0);
	});

	it('waits for delete to finish before closing the dialog and refreshes the list on success', async () => {
		const user = userEvent.setup();
		let resolveDelete: (value: Response) => void;
		const deletePromise = new Promise<Response>((resolve) => { resolveDelete = resolve; });
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockImplementationOnce(() => deletePromise)
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) });
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));

		expect(fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE')).toHaveLength(1);
		expect(screen.getByRole('dialog')).toBeInTheDocument();
		expect(screen.getByRole('button', { name: 'Usuwanie...' })).toBeDisabled();
		expect(screen.getByRole('row', { name: /Burek 2026-05-10 2026-05-12/i })).toBeInTheDocument();

		resolveDelete!(new Response(null, { status: 204 }));

		await waitFor(() => {
			expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
			expect(screen.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeInTheDocument();
		});
		expect(fetch).toHaveBeenCalledTimes(3);
	});

	it('keeps the delete dialog open with a retryable error when deletion fails', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockResolvedValueOnce(new Response(null, { status: 500 }));
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));

		const dialog = screen.getByRole('dialog', { name: /usunąć rezerwację/i });
		expect(dialog).toBeInTheDocument();
		expect(dialog).toHaveTextContent('Nie udało się usunąć rezerwacji. Spróbuj ponownie.');
		expect(screen.getByRole('row', { name: /Burek 2026-05-10 2026-05-12/i })).toBeInTheDocument();
		expect(fetch).toHaveBeenCalledTimes(2);
	});

	it('retries deletion from the same dialog after a retryable failure', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockResolvedValueOnce(new Response(null, { status: 500 }))
			.mockResolvedValueOnce(new Response(null, { status: 204 }))
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) });
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));
		expect(screen.getByRole('dialog', { name: /usunąć rezerwację/i })).toHaveTextContent(
			'Nie udało się usunąć rezerwacji. Spróbuj ponownie.'
		);

		await user.click(screen.getByRole('button', { name: 'Usuń' }));

		await waitFor(() => {
			expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
			expect(screen.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeInTheDocument();
		});
		const deleteCalls = fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE');
		expect(deleteCalls).toHaveLength(2);
	});

	it('cancels the delete dialog after a retryable failure without changing the list', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockResolvedValueOnce(new Response(null, { status: 500 }));
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));
		expect(screen.getByRole('dialog', { name: /usunąć rezerwację/i })).toHaveTextContent(
			'Nie udało się usunąć rezerwacji. Spróbuj ponownie.'
		);

		await user.click(screen.getByRole('button', { name: 'Anuluj' }));

		expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
		expect(screen.getByRole('row', { name: /Burek 2026-05-10 2026-05-12/i })).toBeInTheDocument();
		expect(fetch).toHaveBeenCalledTimes(2);
	});

	it('blocks duplicate delete submissions while deletion is pending', async () => {
		const user = userEvent.setup();
		const deletePromise = new Promise<Response>(() => {});
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockImplementationOnce(() => deletePromise);
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		const confirm = screen.getByRole('button', { name: 'Usuń' });
		await user.click(confirm);
		await user.click(confirm);

		const deleteCalls = fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE');
		expect(deleteCalls).toHaveLength(1);
		expect(screen.getByRole('button', { name: 'Usuwanie...' })).toBeDisabled();
	});

	it('blocks duplicate delete submissions while a retry is pending', async () => {
		const user = userEvent.setup();
		const retryPromise = new Promise<Response>(() => {});
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockResolvedValueOnce(new Response(null, { status: 500 }))
			.mockImplementationOnce(() => retryPromise);
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));
		await user.click(screen.getByRole('button', { name: 'Usuwanie...' }));

		const deleteCalls = fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE');
		expect(deleteCalls).toHaveLength(2);
		expect(screen.getByRole('button', { name: 'Usuwanie...' })).toBeDisabled();
	});

	it('deletes a past reservation through the same dialog workflow', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([
					reservation({
						id: 'local:1',
						dogName: 'Senior',
						startDate: '2025-01-10',
						endDate: '2025-01-12',
						createdAt: '2025-01-09T10:00:00Z'
					})
				]))
			})
			.mockImplementationOnce(() => Promise.resolve(new Response(null, { status: 204 })))
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) });
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Senior' }));

		const dialog = screen.getByRole('dialog', { name: /usunąć rezerwację/i });
		expect(dialog).toHaveTextContent('Senior');

		await user.click(screen.getByRole('button', { name: 'Usuń' }));

		await waitFor(() => {
			expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
		});

		expect(fetch.mock.calls.filter(([, opts]) => opts?.method === 'DELETE')).toHaveLength(1);
	});

	it('treats a 404 delete response as successful absence and refreshes the list', async () => {
		const user = userEvent.setup();
		const fetch = vi.fn()
			.mockResolvedValueOnce({
				ok: true,
				json: () => Promise.resolve(listResponse([reservation()]))
			})
			.mockImplementationOnce(() => Promise.resolve(new Response(null, { status: 404 })))
			.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(listResponse()) });
		vi.stubGlobal('fetch', fetch);

		render(Page);

		await user.click(await screen.findByRole('button', { name: 'Usuń rezerwację dla Burek' }));
		await user.click(screen.getByRole('button', { name: 'Usuń' }));

		await waitFor(() => {
			expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
			expect(screen.getByText('Brak rezerwacji. Dodaj pierwszą powyżej.')).toBeInTheDocument();
		});
		expect(fetch).toHaveBeenCalledTimes(3);
	});

	it('dims past reservations and tags them as finished', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(listResponse([
				reservation({
					id: 'local:1',
					dogName: 'Senior',
					startDate: '2026-04-20',
					endDate: '2026-04-21'
				})
			]))
		}));

		render(Page);

		const row = await screen.findByRole('row', { name: /Senior 2026-04-20 2026-04-21 Lokalna zakończona/i });
		expect(row).toHaveClass('opacity-50');
	});
});

describe('Google Calendar connection status', () => {
	it('shows a connect prompt when Google Calendar is not connected', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(googleListResponse('not_connected'))
		}));

		render(Page);

		const link = await screen.findByRole('link', { name: 'Connect Google Calendar' });
		expect(link).toHaveAttribute('href', '/api/google/login');
	});

	it('shows a reconnect prompt when Google Calendar authorization has expired', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(googleListResponse('unauthorized'))
		}));

		render(Page);

		const link = await screen.findByRole('link', { name: 'Reconnect' });
		expect(link).toHaveAttribute('href', '/api/google/login');
	});

	it('shows a warning banner when Google Calendar source has an error', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(googleListResponse('error'))
		}));

		render(Page);

		expect(await screen.findByRole('alert')).toHaveTextContent(
			'Google Calendar reservations could not be loaded.'
		);
	});

	it('does not show a Google Calendar banner when the source is healthy', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(googleListResponse('ok'))
		}));

		render(Page);

		await screen.findByText(/Brak rezerwacji/);
		expect(screen.queryByRole('alert')).not.toBeInTheDocument();
		expect(screen.queryByRole('link', { name: /google calendar|reconnect/i })).not.toBeInTheDocument();
	});

	it('does not show a Google Calendar banner when the source is not configured', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(googleListResponse('not_configured'))
		}));

		render(Page);

		await screen.findByText(/Brak rezerwacji/);
		expect(screen.queryByRole('alert')).not.toBeInTheDocument();
		expect(screen.queryByRole('link', { name: /google calendar|reconnect/i })).not.toBeInTheDocument();
	});
});
