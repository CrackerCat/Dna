﻿using Dna.Binary;
using Dna.Extensions;
using Dna.Lifting;
using Dna.LLVMInterop.Passes.Matchers;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dna.LLVMInterop.Passes
{
    public class ConstantConcretizationPass
    {
        private readonly LLVMValueRef function;

        private readonly LLVMBuilderRef builder;

        private readonly IBinary binary;

        private readonly Dictionary<ulong, uint> accessedBytes = new();

        private readonly HashSet<string> existingConcretizes = new();

        public ConstantConcretizationPass(LLVMValueRef function, LLVMBuilderRef builder, IBinary binary)
        {
            this.function = function;
            this.builder = builder;
            this.binary = binary;
        }

        public void Execute()
        {

            //function.GlobalParent.PrintToFile("pre_concretization.ll");
            // Get all GEP instructions.
            var instructions = function.GetInstructions();

            var existingStores = instructions.Where(x => x.InstructionOpcode == LLVMOpcode.LLVMStore);
            foreach (var existingStore in existingStores)
            {
                var gep = existingStore.GetOperand(1);
                if (gep.InstructionOpcode != LLVMOpcode.LLVMGetElementPtr)
                    continue;

                var gepIndex = gep.GetOperand(1);
                if (!BinaryAccessMatcher.IsBinarySectionAccess(gepIndex))
                    continue;

                var offset = BinaryAccessMatcher.GetBinarySectionOffset(gepIndex);
                var size = existingStore.GetOperand(0).TypeOf.IntWidth;
                //existingConcretizes.Add($"{offset}_{size}");

                var storeVal = existingStore.GetOperand(0);
                if (storeVal.Kind != LLVMValueKind.LLVMConstantIntValueKind)
                    continue;

                var valConst = storeVal.ConstIntZExt;
                existingConcretizes.Add($"{offset}_{size}_{valConst}");
            }

            // Traverse each instruction and track a set of all bytes being accessed.
            foreach (var instruction in instructions)
            {
                if (instruction.InstructionOpcode == LLVMOpcode.LLVMLoad)
                    TrackSecionAccesses(instruction.GetOperand(0), instruction.TypeOf.IntWidth);
                //else if(instruction.InstructionOpcode == LLVMOpcode.LLVMStore)
                //  TrackSecionAccesses(instruction.GetOperand(1), instruction.GetOperand(0).TypeOf.IntWidth);
            }

            ConcretizeBinarySectionAccesses();

            Optimize();

            //function.GlobalParent.PrintToFile("post_concretization.ll");
            Console.WriteLine("Concretized.");
        }

        private void TrackSecionAccesses(LLVMValueRef value, uint bitWidth)
        {
            // If this is not a getelementptr, then it's a global variable, where no processing is needed.
            if (value.InstructionOpcode != LLVMOpcode.LLVMGetElementPtr)
                return;

            // If this is not a binary section access, then it's not relevant.
            if (!BinaryAccessMatcher.IsBinarySectionAccess(value.GetOperand(1)))
                return;

            // Get the binary section offset.
            var sectionOffset = BinaryAccessMatcher.GetBinarySectionOffset(value.GetOperand(1));

            if(sectionOffset == 0x140042101)
            {
            //   Debugger.Break();
            }

            if (!accessedBytes.ContainsKey(sectionOffset))
            {

                accessedBytes.Add(sectionOffset, bitWidth);
            }

            else
            {
                var currentWidth = accessedBytes[sectionOffset];
                if (bitWidth > currentWidth)
                {
                    accessedBytes[sectionOffset] = bitWidth;
                }
            }
            //accessedBytes.TryAdd(sectionOffset, bitWidth);

            /*
            for (ulong i = sectionOffset; i < sectionOffset + bitWidth; i++)
            {
                accessedBytes.Add(i);
            }
            */
        }

        private void ConcretizeBinarySectionAccesses()
        {
            // Sort the byte addresses in ascending order.
            var byteAddresses = accessedBytes.OrderBy(x => x.Key);


            // Get the memory ptr.
            var memoryPtr = function.FirstBasicBlock.FirstInstruction;
            if (!memoryPtr.ToString().Contains("%0 = load ptr, ptr @memo"))
                throw new InvalidOperationException();

            var last = memoryPtr.NextInstruction;
            byteAddresses.Reverse();

            //last = function.EntryBasicBlock.GetInstructions().First(x => x.ToString().Contains("%sub = add i64 "));

            var readBytes = (ulong address, uint size) =>
            {
                var bytes = binary.ReadBytes(address, (int)size);
                var value = size switch
                {
                    1 => bytes[0],
                    2 => BitConverter.ToUInt16(bytes),
                    4 => BitConverter.ToUInt32(bytes),
                    8 => BitConverter.ToUInt64(bytes),
                    _ => throw new InvalidOperationException()
                };
                return (ulong)value;
            };


            var gsAccess = function.EntryBasicBlock.GetInstructions().First(x => x.OperandCount == 1 && x.GetOperand(0).Kind == LLVMValueKind.LLVMGlobalVariableValueKind
            && x.GetOperand(0).Name == "gs");

            last = gsAccess.NextInstruction;

            foreach (var address in byteAddresses)
            {
                // Since we're iterating in reverse, position the builder before
                // the previously added instruction.
                builder.PositionBefore(last);

                // Create a constant byte integer.
                var context = function.GlobalParent.Context;
                var valType = LLVMTypeRef.CreateInt(address.Value);
                var memValue = readBytes(address.Key, address.Value / 8);

                var name = $"{address.Key}_{address.Value}_{memValue}";

                if (existingConcretizes.Contains(name) && address.Key == 0x140042101)
                {
                   // Debugger.Break();
                }

                if (existingConcretizes.Contains(name))
                {
                    continue;
                }

                var constantInt = LLVMValueRef.CreateConstInt(valType, memValue, false);

                if(address.Key == 0x140042101)
                {
                   // Debugger.Break();
                }
                // Store the constant byte to memory.
                var storeAddr = LLVMValueRef.CreateConstInt(context.Int64Type, address.Key, false);
                var storePtr = builder.BuildInBoundsGEP2(context.Int8Type, memoryPtr, new LLVMValueRef[] { storeAddr });
                //storePtr = builder.BuildBitCast(storePtr, LLVMTypeRef.CreatePointer(valType, 0));

                last = builder.BuildStore(constantInt, storePtr);
            }
        }

        private void Optimize()
        {
            bool optimize = true;
            if (optimize)
            {
                var passManager2 = function.GlobalParent.CreateFunctionPassManager();
                passManager2.AddEarlyCSEPass();
                passManager2.AddNewGVNPass();
                passManager2.InitializeFunctionPassManager();
                for (int i = 0; i < 1; i++)
                {
                    passManager2.RunFunctionPassManager(function);
                }
                passManager2.FinalizeFunctionPassManager();
                Console.WriteLine("ran");
            }
        }
    }
}