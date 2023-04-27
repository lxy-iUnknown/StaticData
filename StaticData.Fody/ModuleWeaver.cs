using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace StaticData.Fody
{
    public partial class ModuleWeaver : BaseModuleWeaver
    {
        enum TypeKind { Boolean = 0, SByte, Byte, Char, Short, UShort, Int, UInt, Long, ULong, Float, Double, Unknown };

        private const string FULL_NAME = "StaticDataAttribute";
        private static readonly Dictionary<string, TypeKind> m_nameToTypeCodeMap = new()
        {
            { "System.Boolean", TypeKind.Boolean },
            { "System.SByte", TypeKind.SByte },
            { "System.Byte", TypeKind.Byte },
            { "System.Char", TypeKind.Char },
            { "System.Int16", TypeKind.Short },
            { "System.UInt16", TypeKind.UShort },
            { "System.Int32", TypeKind.Int },
            { "System.UInt32", TypeKind.UInt },
            { "System.Int64", TypeKind.Long },
            { "System.UInt64", TypeKind.ULong },
            { "System.Single", TypeKind.Float },
            { "System.Double", TypeKind.Double },
        };
        private static readonly TypeData[] m_typeDatas =
        {
            TypeData.Create<bool>(),
            TypeData.Create<sbyte>(),
            TypeData.Create<byte>(),
            TypeData.Create<char>(),
            TypeData.Create<short>(),
            TypeData.Create<ushort>(),
            TypeData.Create<int>(),
            TypeData.Create<uint>(),
            TypeData.Create<long>(),
            TypeData.Create<ulong>(),
            TypeData.Create<float>(),
            TypeData.Create<double>(),
        };
        private static readonly Func<CustomAttribute, bool> m_attributeNameTest =
            (CustomAttribute attribute) => attribute.AttributeType.FullName == FULL_NAME;

        private TypeDefinition m_PrivateImplementationDetails;
        private TypeReference m_ValueType_Type; // ValueType
        private ParameterDefinition m_VoidPtr; // void*
        private ParameterDefinition m_Int32; // int
        private CustomAttribute m_CompilerGenerated_Attribute;
        private bool m_IsPrivateImplementationDetailsCreated;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPrimitiveType(TypeReference type, out TypeKind typeKind)
        {
            return m_nameToTypeCodeMap.TryGetValue(type.FullName, out typeKind);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsReadOnlySpanOfT(TypeReference type, out TypeReference genericType)
        {
            if (type.IsGenericInstance)
            {
                GenericInstanceType gen = type.Cast<GenericInstanceType>();
                var parameters = gen.GenericArguments;
                if (parameters.Count == 1)
                {
                    genericType = parameters[0];
                    return gen.ElementType.FullName == "System.ReadOnlySpan`1";
                }
            }
            genericType = null;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsReturnTypeSupported(TypeReference type, out TypeKind typeKind, out bool isReadOnlySpan)
        {
            TypeReference elementType;
            isReadOnlySpan = false;
            if (type.IsPointer)
            {
                elementType = type.GetElementType();
            }
            else if (IsReadOnlySpanOfT(type, out elementType))
            {
                isReadOnlySpan = true;
            }
            if (elementType != null)
            {
                return IsPrimitiveType(elementType, out typeKind);
            }
            typeKind = TypeKind.Unknown;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsArgumentTypeSupported(TypeReference type, out TypeKind typeKind)
        {
            if (type.IsArray)
            {
                return IsPrimitiveType(type.GetElementType(), out typeKind);
            }
            typeKind = TypeKind.Unknown;
            return false;
        }
        private void FetchInformation()
        {
            const string PRIVATE_IMPLEMENTATION_DETAILS = "<PrivateImplementationDetails>";

            var md = ModuleDefinition;
            var ts = TypeSystem;
            m_ValueType_Type = md.ImportReference(typeof(ValueType));
            m_VoidPtr = new ParameterDefinition(md.ImportReference(typeof(void*)));
            m_Int32 = new ParameterDefinition(ts.Int32Reference);
            m_CompilerGenerated_Attribute = new CustomAttribute(ModuleDefinition.ImportReference(
                typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)), new byte[] { 0x01, 0x00, 0x00, 0x00 });
            var type = md.GetType(null, PRIVATE_IMPLEMENTATION_DETAILS);
            if (type == null)
            {
                // private auto ansi sealed
                type = new TypeDefinition(null, PRIVATE_IMPLEMENTATION_DETAILS, TypeAttributes.AutoLayout |
                    TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.NotPublic, ts.ObjectReference);
                type.CustomAttributes.Add(m_CompilerGenerated_Attribute);
                m_IsPrivateImplementationDetailsCreated = true;
            }
            m_PrivateImplementationDetails = type;
        }
        public override void WriteError(string message, MethodDefinition method)
        {
            base.WriteError($"Error processing {method.FullName}: {message}");
        }
        private TypeDefinition TryFindOrCreateDataType(TypeDefinition privateImpl, int byteSize)
        {
            var ts = TypeSystem;
            if (byteSize == 1)
            {
                return ts.ByteDefinition;
            }
            if (byteSize == 2)
            {
                return ts.Int16Definition;
            }
            if (byteSize == 4)
            {
                return ts.Int32Definition;
            }
            if (byteSize == 8)
            {
                return ts.Int64Definition;
            }
            string typeName = $"__StaticArrayInitTypeSize={byteSize}";
            if (privateImpl.NestedTypes.TryGet((TypeDefinition type) => type.Name == typeName, out TypeDefinition type))
            {
                return type;
            }
            // nested private explicit ansi sealed
            type = new TypeDefinition("", typeName,
                TypeAttributes.ExplicitLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.NestedPrivate,
                m_ValueType_Type)
            {
                PackingSize = 1,
                ClassSize = byteSize
            };
            privateImpl.NestedTypes.Add(type);
            return type;
        }
        private unsafe FieldDefinition GenerateDataField(TypeData typeData, CustomAttributeArgument[] arguments, int length)
        {
            if (length == 0)
            {
                return null;
            }
            var boxFunction = typeData.BoxFunction;
            var byteSize = typeData.ByteSize;
            var data = new byte[length * byteSize];
            for (int i = 0; i < length; i++)
            {
                boxFunction(arguments[i].Value, ref data[i * byteSize]);
            }
            length *= byteSize;
            string hashString = GenerateHash(data, length);
            var privateImpl = m_PrivateImplementationDetails;
            if (!privateImpl.Fields.TryGet((FieldDefinition field) => field.Name == hashString, out var field))
            {
                // assembly static initonly
                field = new FieldDefinition(hashString, FieldAttributes.Assembly | FieldAttributes.Static |
                    FieldAttributes.InitOnly | FieldAttributes.HasFieldRVA,
                    TryFindOrCreateDataType(privateImpl, length))
                {
                    InitialValue = data
                };
                privateImpl.Fields.Add(field);
            }
            return field;
        }
        private void ProcessMethod(MethodDefinition method, CustomAttribute attribute)
        {
            var arguments = attribute.ConstructorArguments;
            if (!method.HasBody)
            {
                WriteWarning($"Method {method.FullName} does not have method body", method);
                return;
            }
            var argument = arguments[0];
            var value = argument.Value;
            if (value is not CustomAttributeArgument[] customArguments)
            {
                return;
            }
            var argumentType = argument.Type;
            var dataLength = customArguments.Length;
            if (!IsArgumentTypeSupported(argumentType, out var argumentTypeKind))
            {
                WriteError($"Custom attribute argument type {argumentType.FullName} is not supported", method);
            }
            var field = GenerateDataField(m_typeDatas[(int)argumentTypeKind], customArguments, dataLength);
            var returnType = method.ReturnType;
            if (!IsReturnTypeSupported(returnType, out var returnTypeKind, out var isReadOnlySpan))
            {
                WriteError($"Property return type {returnType.FullName} is not supported", method);
            }
            if (argumentTypeKind != returnTypeKind)
            {
                WriteError($"Argument element type kind {argumentTypeKind} is not equal to return type kind {returnTypeKind}", method);
            }
            var body = method.Body;
            var il = body.GetILProcessor();
            body.ExceptionHandlers.Clear();
            body.Variables.Clear();
            var debug = method.DebugInformation;
            debug.CustomDebugInformations.Clear();
            debug.Scope = null;
            debug.StateMachineKickOffMethod = null;
            method.CustomDebugInformations.Clear();
            il.Clear();
            if (isReadOnlySpan)
            {
                // ReadOnlySpan<T>.ctor(void*, int)
                var ctor = new MethodReference(".ctor", TypeSystem.VoidReference)
                {
                    ReturnType = TypeSystem.VoidReference,
                    HasThis = true,
                    DeclaringType = returnType,
                };
                ctor.Parameters.Add(m_VoidPtr);
                ctor.Parameters.Add(m_Int32);
                var resolved = ctor.Resolve();
                if (resolved == null)
                {
                    WriteError($"Cannot find {returnType.FullName}.ctor(void*, int) constructor", method);
                }
                if (resolved.IsPrivate)
                {
                    WriteError($"{returnType.FullName}.ctor(void*, int) constructor is private", method);
                }
                if (field == null)
                {
                    // return default(ReadOnlySpan<T>);
                    body.Variables.Add(new VariableDefinition(returnType));
                    il.Emit(OpCodes.Ldloca_S, (byte)0);
                    il.Emit(OpCodes.Initobj, returnType);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    // return new ReadOnlySpan<T>(<field address>, length);
                    il.Emit(OpCodes.Ldsflda, field);
                    if (dataLength > 127)
                    {
                        il.Emit(OpCodes.Ldc_I4, dataLength);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)dataLength);
                    }
                    il.Emit(OpCodes.Newobj, ctor);
                    il.Emit(OpCodes.Ret);
                }
            }
            else
            {
                if (field == null)
                {
                    // return null
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Conv_I);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    // return <field address>
                    il.Emit(OpCodes.Ldsflda, field);
                    il.Emit(OpCodes.Ret);
                }
            }
        }
        private void ProcessType(TypeDefinition type)
        {
            type.Properties.ForEach(property =>
            {
                var getMethod = property.GetMethod;
                var customAttributes = property.CustomAttributes;
                if (customAttributes.TryGet(m_attributeNameTest, out var attribute))
                {
                    if (getMethod == null)
                    {
                        WriteWarning($"Property {property.FullName} does not have get method");
                    }
                    else
                    {
                        ProcessMethod(getMethod, attribute);
                    }
                    customAttributes.Remove(attribute);
                }
            });
            type.Methods.ForEach(method =>
            {
                var customAttributes = method.CustomAttributes;
                if (customAttributes.TryGet(m_attributeNameTest, out var attribute))
                {
                    ProcessMethod(method, attribute);
                    customAttributes.Remove(attribute);
                }
            });
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessTypeAndNestedTypes(TypeDefinition type)
        {
            ProcessType(type);
            type.NestedTypes.ForEach(ProcessTypeAndNestedTypes);
        }
        public override bool ShouldCleanReference => true;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Execute()
        {
            FetchInformation();
            var types = ModuleDefinition.Types;
            types.ForEach(ProcessTypeAndNestedTypes);
            if (m_IsPrivateImplementationDetailsCreated)
            {
                types.Add(m_PrivateImplementationDetails);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new string[] { "mscorlib" };
        }
    }
}