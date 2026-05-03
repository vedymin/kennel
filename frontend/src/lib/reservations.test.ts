import { describe, expect, it, vi } from 'vitest';
import { createReservation, deleteReservation, listReservations } from './reservations';

describe('Reservation API adapter', () => {
	it('returns reservations from the API', async () => {
		const reservations = [
			{
				id: 1,
				dogName: 'Burek',
				startDate: '2026-05-10',
				endDate: '2026-05-12',
				createdAt: '2026-05-02T10:00:00Z'
			}
		];
		const fetcher = vi.fn().mockResolvedValue({
			ok: true,
			json: () => Promise.resolve(reservations)
		});

		await expect(listReservations(fetcher)).resolves.toEqual({
			ok: true,
			reservations
		});
		expect(fetcher).toHaveBeenCalledWith('http://localhost:5174/api/reservations');
	});

	it('returns a failed result when reservations cannot be loaded', async () => {
		const fetcher = vi.fn().mockRejectedValue(new Error('network down'));

		await expect(listReservations(fetcher)).resolves.toEqual({ ok: false });
	});

	it('creates a reservation through the API', async () => {
		const request = {
			dogName: 'Burek',
			startDate: '2026-05-10',
			endDate: '2026-05-12'
		};
		const fetcher = vi.fn().mockResolvedValue(new Response(null, { status: 201 }));

		await expect(createReservation(request, fetcher)).resolves.toEqual({ ok: true });
		expect(fetcher).toHaveBeenCalledWith('http://localhost:5174/api/reservations', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(request)
		});
	});

	it('returns validation errors when reservation creation is rejected', async () => {
		const errors = { dogName: 'Imie psa jest wymagane.' };
		const fetcher = vi.fn().mockResolvedValue(
			new Response(JSON.stringify({ errors }), {
				status: 400,
				headers: { 'Content-Type': 'application/json' }
			})
		);

		await expect(
			createReservation({ dogName: '', startDate: '2026-05-10', endDate: '2026-05-12' }, fetcher)
		).resolves.toEqual({ ok: false, errors });
	});

	it('returns a failed result when reservation creation cannot reach the API', async () => {
		const fetcher = vi.fn().mockRejectedValue(new Error('network down'));

		await expect(
			createReservation({ dogName: 'Burek', startDate: '2026-05-10', endDate: '2026-05-12' }, fetcher)
		).resolves.toEqual({ ok: false });
	});

	it('deletes a reservation through the API', async () => {
		const fetcher = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));

		await expect(deleteReservation(7, fetcher)).resolves.toEqual({ ok: true });
		expect(fetcher).toHaveBeenCalledWith('http://localhost:5174/api/reservations/7', {
			method: 'DELETE'
		});
	});

	it('treats a missing reservation as successfully absent', async () => {
		const fetcher = vi.fn().mockResolvedValue(new Response(null, { status: 404 }));

		await expect(deleteReservation(7, fetcher)).resolves.toEqual({ ok: true });
	});

	it('returns a failed result when deletion cannot reach the API', async () => {
		const fetcher = vi.fn().mockRejectedValue(new Error('network down'));

		await expect(deleteReservation(7, fetcher)).resolves.toEqual({ ok: false });
	});
});
