Operator {
	identifier: string
	priority: num

	this == other => identifier == other.identifier && priority == other.priority
}

List {
	private elements: Item[]

	List(capacity: num) {
		elements = Item[capacity]
	}

	List() {
		elements = Item[1024]
	}
}

Map { Key, Value } : { Iterable, Disposable } { 

}

Map {
	keys: Key List
	values: Value List

	init(Key: type, Value: type)

	get(key: Key) {
		i = keys.index_of(key)
		
		if i == -1 {
			fail 'Couldn\'t find a value paired with the given key'
		}

		=> values[i]
	}
}

Lexer {
	operators: Operator List
	variables: string Variable Map
	
}

# Idea: Project or module wide include

.to{T}() => this as T

inline assert(x) => if !x => fail

generate(chunks: List<Chunk>) {
	loop chunk in chunks expect 1s {
		chunk.generate()
		lend chunks
	}
}

bake(chunks: List<Chunk>) {
	loop chunk in chunks expect 1s {
		chunk.create()
		lend chunks
	}
}

f() {
	async loop (i = 0, i < 10, i++) {
		println(i)
	}

	# vs.

	loop... (i = 0, i < 10, i++) {
		println(i)
	}

	inline buffer = num[100]

	loop (i = 0, i < 100, i++) {
		buffer[i] = buffer[i] * 2
	}

	if... GetReponseCode() == ReponseCode.OK {
		SendFile('./index.html')
	}

	# also:

	if GetReponseCode() == ReponseCode.OK {
		SendFile('./index.html')
	}...

	keyword_operator = operator.to(KeywordOperator)

	operators = List(Operator)
	operators = List(Operator, 100)
	operators = Operator List
	operators = Operator List(100)

	operator_map = Map(string, Operator)
	operator_map = Map(string, Operator, 100)

	operator_map_list = List(Map(string, Operator))

}

run(lexer: Lexer) {
	async_operator = Operator {
		identifier = '...'
		priority = 2
	}

	lexer.operators.add(async_operator)
}