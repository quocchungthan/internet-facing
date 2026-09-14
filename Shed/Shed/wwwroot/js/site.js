(() => {
	const appShell = document.querySelector('.app-shell');
	const sideNav = document.getElementById('sideNav');
	const toggleButton = document.getElementById('sideNavToggle');
	const mobileToggle = document.getElementById('mobileNavToggle');
	if (!appShell || !toggleButton) {
		return;
	}

	const storageKey = 'side_nav_collapsed';
	const isMobile = () => window.innerWidth <= 880;

	const setState = (collapsed) => {
		if (isMobile()) return; // mobile handled separately
		appShell.classList.toggle('is-collapsed', collapsed);
		toggleButton.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
		toggleButton.setAttribute('title', collapsed ? 'Expand menu' : 'Collapse menu');
		toggleButton.textContent = collapsed ? '▤' : '☰';
	};

	const initialCollapsed = window.localStorage.getItem(storageKey) === '1';
	setState(initialCollapsed);

	toggleButton.addEventListener('click', () => {
		if (isMobile()) return;
		const collapsed = !appShell.classList.contains('is-collapsed');
		setState(collapsed);
		window.localStorage.setItem(storageKey, collapsed ? '1' : '0');
	});

	// Mobile overlay toggle
	if (mobileToggle && sideNav) {
		const openMobileNav = () => {
			sideNav.classList.add('is-mobile-open');
			mobileToggle.setAttribute('aria-expanded', 'true');
			if (toggleButton) toggleButton.textContent = '✕';
		};
		const closeMobileNav = () => {
			sideNav.classList.remove('is-mobile-open');
			mobileToggle.setAttribute('aria-expanded', 'false');
			if (toggleButton) toggleButton.textContent = '☰';
		};

		mobileToggle.addEventListener('click', () => {
			if (sideNav.classList.contains('is-mobile-open')) {
				closeMobileNav();
			} else {
				openMobileNav();
			}
		});

		// The desktop toggle button (#sideNavToggle) lives INSIDE the overlay on mobile.
		// When the overlay is open the hamburger in the topbar is hidden beneath it,
		// so wire the inner toggle to close the nav on mobile too.
		if (toggleButton) {
			toggleButton.addEventListener('click', () => {
				if (isMobile()) closeMobileNav();
			});
		}

		// Clicking the overlay background (not a nav link/button) closes the nav.
		sideNav.addEventListener('click', (e) => {
			if (!isMobile()) return;
			// Only close when the click target is the nav root itself (backdrop area).
			if (e.target === sideNav) closeMobileNav();
		});

		// Close when a nav link is clicked
		sideNav.querySelectorAll('.nav-link').forEach(link => {
			link.addEventListener('click', closeMobileNav);
		});

		// Close on Escape
		document.addEventListener('keydown', (e) => {
			if (e.key === 'Escape') closeMobileNav();
		});
	}
})();
