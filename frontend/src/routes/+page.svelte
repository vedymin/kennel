<script lang="ts">
	import {
		createReservation,
		deleteReservation,
		listReservations,
		type Reservation,
		type ReservationSources
	} from '$lib/reservations';

	let dogName = $state('');
	let startDate = $state('');
	let endDate = $state('');
	let submitting = $state(false);
	let errors = $state<Record<string, string>>({});
	let reservations = $state<Reservation[]>([]);
	let loading = $state(true);
	let fetchError = $state(false);
	let sources = $state<ReservationSources>({});
	let reservationToDelete = $state<Reservation | null>(null);
	let deleting = $state(false);
	let deleteError = $state('');
	let unhealthySources = $derived(
		Object.entries(sources).filter(([, source]) => source.status !== 'ok')
	);

	const canSubmit = $derived(dogName.trim() !== '' && startDate !== '' && endDate !== '');

	async function loadReservations() {
		loading = true;
		fetchError = false;
		const result = await listReservations();
		if (result.ok) {
			reservations = result.reservations;
			sources = result.sources;
		} else {
			fetchError = true;
		}
		loading = false;
	}

	async function handleSubmit() {
		submitting = true;
		errors = {};

		const result = await createReservation({ dogName, startDate, endDate });
		if (result.ok) {
			dogName = '';
			startDate = '';
			endDate = '';
			await loadReservations();
		} else if (result.errors) {
			errors = result.errors;
		} else {
			errors = { _form: 'Nie udało się wysłać formularza.' };
		}
		submitting = false;
	}

	function isPast(reservation: Reservation): boolean {
		const today = new Date().toISOString().split('T')[0];
		return reservation.endDate < today;
	}

	function sourceLabel(source: string): string {
		if (source === 'local') return 'Lokalna';
		if (source === 'google') return 'Google';
		return source;
	}

	function sourceStatusMessage(source: string, status: string): string {
		return `Źródło ${sourceLabel(source)} ma status ${status}.`;
	}

	function requestDelete(reservation: Reservation) {
		reservationToDelete = reservation;
		deleteError = '';
	}

	function cancelDelete() {
		if (deleting) return;
		reservationToDelete = null;
	}

	async function confirmDelete() {
		if (!reservationToDelete || deleting) return;

		deleting = true;
		deleteError = '';
		const result = await deleteReservation(reservationToDelete.id);
		if (result.ok) {
			reservationToDelete = null;
			await loadReservations();
		} else {
			deleteError = 'Nie udało się usunąć rezerwacji. Spróbuj ponownie.';
		}
		deleting = false;
	}

	$effect(() => {
		loadReservations();
	});
</script>

<main class="max-w-2xl mx-auto p-6">
	<h1 class="text-2xl font-bold mb-6">Hotel dla psów — rezerwacje</h1>

	<form
		onsubmit={(e) => { e.preventDefault(); handleSubmit(); }}
		class="flex flex-wrap gap-3 items-end mb-8"
	>
		<div class="flex flex-col">
			<label for="dogName" class="text-sm font-medium mb-1">Imię psa</label>
			<input
				id="dogName"
				type="text"
				bind:value={dogName}
				maxlength={50}
				class="border rounded px-3 py-2"
			/>
		</div>

		<div class="flex flex-col">
			<label for="startDate" class="text-sm font-medium mb-1">Od</label>
			<input
				id="startDate"
				type="date"
				bind:value={startDate}
				class="border rounded px-3 py-2"
			/>
		</div>

		<div class="flex flex-col">
			<label for="endDate" class="text-sm font-medium mb-1">Do</label>
			<input
				id="endDate"
				type="date"
				bind:value={endDate}
				class="border rounded px-3 py-2"
			/>
		</div>

		<button
			type="submit"
			disabled={!canSubmit || submitting}
			class="bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50"
		>
			{submitting ? 'Dodawanie...' : 'Dodaj'}
		</button>
	</form>

	<div aria-live="polite" class="text-red-600 text-sm mb-4">
		{#if Object.keys(errors).length > 0}
			{#each Object.values(errors) as msg}
				<p>{msg}</p>
			{/each}
		{/if}
	</div>

	{#if unhealthySources.length > 0}
		<div role="status" class="mb-4 rounded border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900">
			{#each unhealthySources as [source, health]}
				<p>{sourceStatusMessage(source, health.status)}</p>
			{/each}
		</div>
	{/if}

	{#if loading}
		<p>Ładowanie...</p>
	{:else if fetchError}
		<div>
			<p>Nie udało się pobrać rezerwacji.</p>
			<button onclick={loadReservations} class="text-blue-600 underline">Spróbuj ponownie</button>
		</div>
	{:else if reservations.length === 0}
		<p class="text-gray-500">Brak rezerwacji. Dodaj pierwszą powyżej.</p>
	{:else}
		<table class="w-full text-left border-collapse">
			<thead>
				<tr class="border-b">
					<th class="py-2">Imię psa</th>
					<th class="py-2">Od</th>
					<th class="py-2">Do</th>
					<th class="py-2">Źródło</th>
					<th class="py-2">Akcje</th>
				</tr>
			</thead>
			<tbody>
				{#each reservations as r (r.id)}
					<tr class="border-b {isPast(r) ? 'opacity-50' : ''}">
						<td class="py-2">{r.dogName}</td>
						<td class="py-2">{r.startDate}</td>
						<td class="py-2">{r.endDate}</td>
						<td class="py-2">{sourceLabel(r.source)}</td>
						<td class="py-2">
							<div class="flex items-center gap-3">
								<span>{isPast(r) ? 'zakończona' : ''}</span>
								{#if r.canDelete}
									<button
										type="button"
										aria-label={`Usuń rezerwację dla ${r.dogName}`}
										onclick={() => requestDelete(r)}
										class="text-red-700 underline"
									>
										Usuń
									</button>
								{/if}
							</div>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
	{/if}

	{#if reservationToDelete}
		<div class="fixed inset-0 bg-black/40 flex items-center justify-center p-6">
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="delete-reservation-title"
				class="w-full max-w-sm rounded bg-white p-6 shadow-lg"
			>
				<h2 id="delete-reservation-title" class="text-lg font-semibold mb-3">
					Usunąć rezerwację?
				</h2>
				<p class="mb-6">
					Czy na pewno usunąć rezerwację dla {reservationToDelete.dogName}?
				</p>
				{#if deleteError}
					<p class="mb-4 text-sm text-red-700" aria-live="polite">{deleteError}</p>
				{/if}
				<div class="flex justify-end gap-3">
					<button
						type="button"
						onclick={cancelDelete}
						disabled={deleting}
						class="border rounded px-4 py-2 disabled:opacity-50"
					>
						Anuluj
					</button>
					<button
						type="button"
						onclick={confirmDelete}
						disabled={deleting}
						class="bg-red-700 text-white rounded px-4 py-2 disabled:opacity-50"
					>
						{deleting ? 'Usuwanie...' : 'Usuń'}
					</button>
				</div>
			</div>
		</div>
	{/if}
</main>
