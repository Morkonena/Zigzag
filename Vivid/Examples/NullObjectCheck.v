none = 0

B {
	dummy

	sum(a, b) {
		dummy = a
		=> a + b
	}
}

A {
	other

	init(b) {
		other = b
	}
}

f(primary) {
	if primary != none and primary.other != none {
		primary.other.dummy += 2
		primary.other.dummy -= 2
		=> primary.other.sum(1, 2)
	}
	
	=> 0
}

init() {
	a = A(B())
	f(a)
}