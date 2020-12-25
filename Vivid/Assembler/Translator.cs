using System.Collections.Generic;
using System.Linq;
using System;

public static class Translator
{
	private static List<Register> GetAllUsedNonVolatileRegisters(Unit unit)
	{
		return unit.Instructions.SelectMany(i => i.Parameters).Where(p => p.IsAnyRegister && !p.Value!.To<RegisterHandle>().Register.IsVolatile).Select(p => p.Value!.To<RegisterHandle>().Register).Distinct().ToList();
	}

	private static IEnumerable<Handle> GetAllHandles(Result[] results)
	{
		return results.Select(i => i.Value).Concat(results.SelectMany(i => GetAllHandles(i.Value.GetInnerResults())));
	}

	private static IEnumerable<Handle> GetAllHandles(Unit unit)
	{
		var handles = unit.Instructions.SelectMany(i => i.Parameters.Select(p => p.Value ?? throw new ApplicationException("Instruction parameter was not assigned")));
		
		return handles.Concat(handles.SelectMany(i => GetAllHandles(i.GetInnerResults())));
	}

	private static List<Variable> GetAllSavedLocalVariables(Unit unit)
	{
		return GetAllHandles(unit)
			.Where(h => h is StackVariableHandle v && v.Variable.IsPredictable && v.Variable.LocalAlignment == null)
			.Select(h => h.To<StackVariableHandle>().Variable)
			.Distinct()
			.ToList();
	}

	private static List<TemporaryMemoryHandle> GetAllTemporaryMemoryHandles(Unit unit)
	{
		return GetAllHandles(unit)
			.Where(h => h is TemporaryMemoryHandle)
			.Select(h => h.To<TemporaryMemoryHandle>())
			.ToList();
	}

	private static List<InlineHandle> GetAllInlineHandles(Unit unit)
	{
		return GetAllHandles(unit)
			.Where(h => h is InlineHandle)
			.Select(h => h.To<InlineHandle>())
			.ToList();
	}

	private static List<ConstantDataSectionHandle> GetAllConstantDataSectionHandles(Unit unit)
	{
		return GetAllHandles(unit)
			.Where(h => h is ConstantDataSectionHandle)
			.Select(h => h.To<ConstantDataSectionHandle>())
			.ToList();
	}

	private static void AllocateConstantDataHandles(Unit unit, List<ConstantDataSectionHandle> constant_data_section_handles)
	{
		while (constant_data_section_handles.Count > 0)
		{
			var current = constant_data_section_handles.First();
			var copies = constant_data_section_handles.Where(c => c.Equals(current)).ToList();

			var identifier = unit.GetNextConstantIdentifier(current.Value);
			copies.ForEach(c => c.Identifier = identifier);
			copies.ForEach(c => constant_data_section_handles.Remove(c));
		}
	}

	public static string Translate(Unit unit, List<ConstantDataSectionHandle> constants)
	{
		if (Analysis.IsInstructionAnalysisEnabled)
		{
			InstructionAnalysis.Optimize(unit);
		}
		
		var registers = GetAllUsedNonVolatileRegisters(unit);
		var local_variables = GetAllSavedLocalVariables(unit);
		var temporary_handles = GetAllTemporaryMemoryHandles(unit);
		var inline_handles = GetAllInlineHandles(unit);
		var constant_handles = GetAllConstantDataSectionHandles(unit);

		// When debugging mode is enabled, the base pointer is reserved for saving the value of the stack pointer in the start
		if (Assembler.IsDebuggingEnabled)
		{
			registers.Add(unit.GetBasePointer());	
		}

		var required_local_memory = local_variables.Sum(i => i.Type!.ReferenceSize) + temporary_handles.Sum(i => i.Size.Bytes) + inline_handles.Distinct().Sum(i => i.Bytes);
		var local_memory_top = 0;

		unit.Execute(UnitPhase.BUILD_MODE, () =>
		{
			if (unit.Instructions.Last().Type != InstructionType.RETURN)
			{
				unit.Append(new ReturnInstruction(unit, null, Types.UNKNOWN));
			}

			if (Assembler.IsDebuggingEnabled)
			{
				unit.Append(new LabelInstruction(unit, new Label(Debug.GetEnd(unit.Function).Name)));
			}
		});

		unit.Simulate(UnitPhase.READ_ONLY_MODE, i =>
		{
			if (!i.Is(InstructionType.INITIALIZE)) return;

			var initialization = i.To<InitializeInstruction>();

			initialization.Build(registers, required_local_memory);
			local_memory_top = initialization.LocalMemoryTop;
		});
		
		registers.Reverse();

		unit.Simulate(UnitPhase.READ_ONLY_MODE, i =>
		{
			if (!i.Is(InstructionType.RETURN)) return;

			i.To<ReturnInstruction>().Build(registers, local_memory_top);
		});

		// Align all used local variables
		Aligner.AlignLocalMemory(local_variables, temporary_handles.ToList(), inline_handles, local_memory_top);

		AllocateConstantDataHandles(unit, new List<ConstantDataSectionHandle>(constant_handles));

		unit.Simulate(UnitPhase.BUILD_MODE, instruction =>
		{
			instruction.Translate();
		});

		// Remove duplicates
		constants.AddRange(constant_handles.Distinct());

		return unit.Export();
	}
}