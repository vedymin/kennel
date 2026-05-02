import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import Page from './+page.svelte';

const API = 'http://localhost:5174/api/reservations';

beforeEach(() => {
	vi.restoreAllMocks();
});

describe('Reservation form', () => {
	it('disables submit button when any field is empty', async () => {
		vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve([])
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
			return Promise.resolve({ ok: true, json: () => Promise.resolve([]) });
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

		resolvePost!(new Response(JSON.stringify({ id: 1, dogName: 'Burek', startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() }), {
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
			return Promise.resolve({ ok: true, json: () => Promise.resolve([]) });
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

	it('clears form on successful submission', async () => {
		const user = userEvent.setup();
		const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0];
		const dayAfter = new Date(Date.now() + 172800000).toISOString().split('T')[0];

		vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, opts?: RequestInit) => {
			if (opts?.method === 'POST') {
				return Promise.resolve({
					ok: true,
					status: 201,
					json: () => Promise.resolve({ id: 1, dogName: 'Burek', startDate: tomorrow, endDate: dayAfter, createdAt: new Date().toISOString() })
				});
			}
			return Promise.resolve({ ok: true, json: () => Promise.resolve([]) });
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
});
