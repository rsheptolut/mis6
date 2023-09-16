================= Character set (64 characters = 6 bit bytes)
0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ .,!:;'"+-*/\=&|()[]<>{}@#$^

================= CPU

	The following instructions are supported:
	
		A>%xx  --- %xx = A
		A<%xx  --- A = %xx
		A&%xx  --- A = A & %xx
		A|%xx  --- A = A | %xx
		A^%xx  --- A = A ^ %xx
		A!     --- A = !A
		A+%xx  --- A = A + %xx
		A-%xx  --- A = A - %xx
		A*%xx  --- A = A * %xx
		A/%xx  --- A = A / %xx
		A\%xx  --- A = A MOD %xx
		@%xx  --- IP = %xx
		=%xx  --- if (ZERO) IP = %xx
		#%xx  --- STACK.PUSH(IP + 4); IP = %xx
		A<:   --- %xx = STACK.POP()
		A>:   --- 

	Legend:
	
		First comes the optional register parameter A. It can be anything from A to Z. It's a register that is also mapped to memory addresses 0A to 0Z. You can also specify two registers in sequence to do a word operation instead of a byte operation. For example, AB. This simply sets the current register and is not directly linked to the operation code, and may be omitted if you're using the same register.
		
		Then comes the operation code (listed above). One character always.
		
		%xx is an operation parameter.
		
		% can be one of:
			' - a byte value follows
			" - a word value follows
			$ - a word variable address follows
			* - a word pointer variable address follows (an address at this address will be used)
			@ - a word variable address in ROM memory space follows
			: - special parameter: stack. Refers to memory address 0: which is a memory-mapped function that does pop or push depending on whether you're doing store or load to/from it. Can work on single or double registers.
			

================ RAM-mapped features

	00: Discard
	05: Console
	06: RP(High)
	07: RP(Low)
	08: Zero flag
	09: Overflow flag
	0:: Stack pop/push
	0;: Stach pop/push (same exact function)
	0(: IP.High
	0): IP.Low
	0[: Stack.High
	0]: Stack.Low

============== How to use functions / subroutines

	// How to call a function
	
		:A    // You can pass parameters either as registers (then you don't need to do anything, just prepare registers), or push anything to stack as parameters
			  // .. do this as many times as you need to get all the parameters. Be sure the function is expecting exactly as many parameters
		=#xx  // Go to function at xx. This pushes the ret address to the stack and jumps to xx

	// How to declare a function

		:X :Y // Push any registers that are going to be used, to stack (unless they're used as a variable parameter)
		X<#3 X+#2 X>$03 // StackOffset1 = 3 parameter bytes + 2 ret bytes = 5
		X<#0 X>$04 // StackOffset2 = 0. Now Stack offset is set up to point at the last parameter passed via the stack
		X<$01Y<$02 Z+$03Z+$04 // Read stack pointer into XY registers and then add offsets to Z. Z is a special register that actually adds to Y, but any remainder is added to X automatically
		
		// Now Z is pointing to last variable pushed to the stack by the caller, if any.
		// Your function body goes here
		
		// If your function wants to return a value, it can do so in R (or RQ if it's an address)
		R<$xx // Will return a value that's stored at xx
		
		// Function finalization below
		
		.Y .X // Restore any registers that were used from stack
		.$07.$06 // Pop Ret from stack to Ret
		. // pop parameters as many times as there are parameters
		@$06 // Go back to the caller

