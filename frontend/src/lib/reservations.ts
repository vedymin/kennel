const RESERVATIONS_API = 'http://localhost:5174/api/reservations';

export interface Reservation {
	id: number;
	dogName: string;
	startDate: string;
	endDate: string;
	createdAt: string;
}

export type ListReservationsResult =
	| { ok: true; reservations: Reservation[] }
	| { ok: false };

export interface CreateReservationRequest {
	dogName: string;
	startDate: string;
	endDate: string;
}

export type CreateReservationResult =
	| { ok: true }
	| { ok: false; errors?: Record<string, string> };

export type DeleteReservationResult =
	| { ok: true }
	| { ok: false };

export async function listReservations(fetcher: typeof fetch = fetch): Promise<ListReservationsResult> {
	try {
		const response = await fetcher(RESERVATIONS_API);

		if (!response.ok) {
			return { ok: false };
		}

		return {
			ok: true,
			reservations: await response.json()
		};
	} catch {
		return { ok: false };
	}
}

export async function deleteReservation(
	id: number,
	fetcher: typeof fetch = fetch
): Promise<DeleteReservationResult> {
	try {
		const response = await fetcher(`${RESERVATIONS_API}/${id}`, { method: 'DELETE' });

		return { ok: response.ok || response.status === 404 };
	} catch {
		return { ok: false };
	}
}

export async function createReservation(
	request: CreateReservationRequest,
	fetcher: typeof fetch = fetch
): Promise<CreateReservationResult> {
	try {
		const response = await fetcher(RESERVATIONS_API, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(request)
		});

		if (response.ok) {
			return { ok: true };
		}

		if (response.status === 400) {
			const body = await response.json();
			return {
				ok: false,
				errors: body.errors ?? {}
			};
		}

		return { ok: false };
	} catch {
		return { ok: false };
	}
}
