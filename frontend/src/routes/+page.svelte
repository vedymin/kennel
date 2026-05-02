<script lang="ts">
	const API = 'http://localhost:5174/api/reservations';

	interface Reservation {
		id: number;
		dogName: string;
		startDate: string;
		endDate: string;
		createdAt: string;
	}

	let dogName = $state('');
	let startDate = $state('');
	let endDate = $state('');
	let submitting = $state(false);
	let errors = $state<Record<string, string>>({});
	let reservations = $state<Reservation[]>([]);
	let loading = $state(true);
	let fetchError = $state(false);

	const canSubmit = $derived(dogName.trim() !== '' && startDate !== '' && endDate !== '');

	async function loadReservations() {
		loading = true;
		fetchError = false;
		try {
			const res = await fetch(API);
			if (res.ok) {
				reservations = await res.json();
			} else {
				fetchError = true;
			}
		} catch {
			fetchError = true;
		} finally {
			loading = false;
		}
	}

	async function handleSubmit() {
		submitting = true;
		errors = {};

		try {
			const res = await fetch(API, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ dogName, startDate, endDate })
			});

			if (res.ok) {
				dogName = '';
				startDate = '';
				endDate = '';
				await loadReservations();
			} else if (res.status === 400) {
				const data = await res.json();
				errors = data.errors ?? {};
			}
		} catch {
			errors = { _form: 'Nie udało się wysłać formularza.' };
		} finally {
			submitting = false;
		}
	}

	function isPast(reservation: Reservation): boolean {
		const today = new Date().toISOString().split('T')[0];
		return reservation.endDate < today;
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
					<th class="py-2"></th>
				</tr>
			</thead>
			<tbody>
				{#each reservations as r (r.id)}
					<tr class="border-b {isPast(r) ? 'opacity-50' : ''}">
						<td class="py-2">{r.dogName}</td>
						<td class="py-2">{r.startDate}</td>
						<td class="py-2">{r.endDate}</td>
						<td class="py-2">{isPast(r) ? 'zakończona' : ''}</td>
					</tr>
				{/each}
			</tbody>
		</table>
	{/if}
</main>
