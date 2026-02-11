#define ICALL_TABLE_corlib 1

static int corlib_icall_indexes [] = {
186,
199,
200,
201,
202,
203,
204,
205,
206,
207,
210,
211,
212,
385,
386,
387,
415,
416,
417,
444,
445,
446,
563,
564,
565,
568,
604,
605,
607,
609,
611,
613,
618,
626,
627,
628,
629,
630,
631,
632,
633,
634,
635,
636,
637,
638,
639,
640,
641,
642,
644,
645,
646,
647,
648,
649,
650,
741,
742,
743,
744,
745,
746,
747,
748,
749,
750,
751,
752,
753,
754,
755,
756,
757,
759,
760,
761,
762,
763,
764,
765,
827,
836,
837,
907,
913,
916,
918,
923,
924,
926,
927,
931,
933,
934,
936,
937,
940,
941,
942,
945,
947,
950,
952,
954,
963,
1030,
1032,
1034,
1044,
1045,
1046,
1048,
1054,
1055,
1056,
1057,
1058,
1066,
1067,
1068,
1072,
1073,
1075,
1079,
1080,
1081,
1373,
1566,
1567,
8373,
8374,
8376,
8377,
8378,
8379,
8380,
8382,
8383,
8384,
8385,
8386,
8404,
8406,
8411,
8413,
8415,
8417,
8466,
8472,
8473,
8475,
8476,
8477,
8478,
8479,
8481,
8483,
9659,
9663,
9665,
9666,
9667,
9668,
10098,
10099,
10100,
10101,
10121,
10122,
10123,
10168,
10249,
10252,
10260,
10261,
10262,
10263,
10264,
10583,
10584,
10589,
10590,
10623,
10659,
10666,
10673,
10684,
10688,
10713,
10797,
10799,
10809,
10811,
10812,
10813,
10820,
10835,
10855,
10856,
10864,
10866,
10873,
10874,
10877,
10879,
10884,
10890,
10891,
10898,
10900,
10912,
10915,
10916,
10917,
10928,
10938,
10944,
10945,
10946,
10948,
10949,
10966,
10968,
10983,
11003,
11004,
11029,
11034,
11064,
11065,
11673,
11759,
11760,
11973,
11974,
11982,
11983,
11984,
11989,
12045,
12470,
12471,
12693,
12694,
12700,
12710,
14120,
14141,
14143,
14145,
};
void ves_icall_System_Array_InternalCreate (int,int,int,int,int);
int ves_icall_System_Array_GetCorElementTypeOfElementTypeInternal (int);
int ves_icall_System_Array_IsValueOfElementTypeInternal (int,int);
int ves_icall_System_Array_CanChangePrimitive (int,int,int);
int ves_icall_System_Array_FastCopy (int,int,int,int,int);
int ves_icall_System_Array_GetLengthInternal_raw (int,int,int);
int ves_icall_System_Array_GetLowerBoundInternal_raw (int,int,int);
void ves_icall_System_Array_GetGenericValue_icall (int,int,int);
void ves_icall_System_Array_GetValueImpl_raw (int,int,int,int);
void ves_icall_System_Array_SetGenericValue_icall (int,int,int);
void ves_icall_System_Array_SetValueImpl_raw (int,int,int,int);
void ves_icall_System_Array_InitializeInternal_raw (int,int);
void ves_icall_System_Array_SetValueRelaxedImpl_raw (int,int,int,int);
void ves_icall_System_Runtime_RuntimeImports_ZeroMemory (int,int);
void ves_icall_System_Runtime_RuntimeImports_Memmove (int,int,int);
void ves_icall_System_Buffer_BulkMoveWithWriteBarrier (int,int,int,int);
int ves_icall_System_Delegate_AllocDelegateLike_internal_raw (int,int);
int ves_icall_System_Delegate_CreateDelegate_internal_raw (int,int,int,int,int);
int ves_icall_System_Delegate_GetVirtualMethod_internal_raw (int,int);
void ves_icall_System_Enum_GetEnumValuesAndNames_raw (int,int,int,int);
int ves_icall_System_Enum_InternalGetCorElementType (int);
void ves_icall_System_Enum_InternalGetUnderlyingType_raw (int,int,int);
int ves_icall_System_Environment_get_ProcessorCount ();
int ves_icall_System_Environment_get_TickCount ();
int64_t ves_icall_System_Environment_get_TickCount64 ();
void ves_icall_System_Environment_FailFast_raw (int,int,int,int);
void ves_icall_System_GC_register_ephemeron_array_raw (int,int);
int ves_icall_System_GC_get_ephemeron_tombstone_raw (int);
void ves_icall_System_GC_SuppressFinalize_raw (int,int);
void ves_icall_System_GC_ReRegisterForFinalize_raw (int,int);
void ves_icall_System_GC_GetGCMemoryInfo (int,int,int,int,int,int);
int ves_icall_System_GC_AllocPinnedArray_raw (int,int,int);
int ves_icall_System_Object_MemberwiseClone_raw (int,int);
double ves_icall_System_Math_Acos (double);
double ves_icall_System_Math_Acosh (double);
double ves_icall_System_Math_Asin (double);
double ves_icall_System_Math_Asinh (double);
double ves_icall_System_Math_Atan (double);
double ves_icall_System_Math_Atan2 (double,double);
double ves_icall_System_Math_Atanh (double);
double ves_icall_System_Math_Cbrt (double);
double ves_icall_System_Math_Ceiling (double);
double ves_icall_System_Math_Cos (double);
double ves_icall_System_Math_Cosh (double);
double ves_icall_System_Math_Exp (double);
double ves_icall_System_Math_Floor (double);
double ves_icall_System_Math_Log (double);
double ves_icall_System_Math_Log10 (double);
double ves_icall_System_Math_Pow (double,double);
double ves_icall_System_Math_Sin (double);
double ves_icall_System_Math_Sinh (double);
double ves_icall_System_Math_Sqrt (double);
double ves_icall_System_Math_Tan (double);
double ves_icall_System_Math_Tanh (double);
double ves_icall_System_Math_FusedMultiplyAdd (double,double,double);
double ves_icall_System_Math_Log2 (double);
double ves_icall_System_Math_ModF (double,int);
float ves_icall_System_MathF_Acos (float);
float ves_icall_System_MathF_Acosh (float);
float ves_icall_System_MathF_Asin (float);
float ves_icall_System_MathF_Asinh (float);
float ves_icall_System_MathF_Atan (float);
float ves_icall_System_MathF_Atan2 (float,float);
float ves_icall_System_MathF_Atanh (float);
float ves_icall_System_MathF_Cbrt (float);
float ves_icall_System_MathF_Ceiling (float);
float ves_icall_System_MathF_Cos (float);
float ves_icall_System_MathF_Cosh (float);
float ves_icall_System_MathF_Exp (float);
float ves_icall_System_MathF_Floor (float);
float ves_icall_System_MathF_Log (float);
float ves_icall_System_MathF_Log10 (float);
float ves_icall_System_MathF_Pow (float,float);
float ves_icall_System_MathF_Sin (float);
float ves_icall_System_MathF_Sinh (float);
float ves_icall_System_MathF_Sqrt (float);
float ves_icall_System_MathF_Tan (float);
float ves_icall_System_MathF_Tanh (float);
float ves_icall_System_MathF_FusedMultiplyAdd (float,float,float);
float ves_icall_System_MathF_Log2 (float);
float ves_icall_System_MathF_ModF (float,int);
int ves_icall_RuntimeMethodHandle_GetFunctionPointer_raw (int,int);
void ves_icall_RuntimeMethodHandle_ReboxFromNullable_raw (int,int,int);
void ves_icall_RuntimeMethodHandle_ReboxToNullable_raw (int,int,int,int);
int ves_icall_RuntimeType_GetCorrespondingInflatedMethod_raw (int,int,int);
void ves_icall_RuntimeType_make_array_type_raw (int,int,int,int);
void ves_icall_RuntimeType_make_byref_type_raw (int,int,int);
void ves_icall_RuntimeType_make_pointer_type_raw (int,int,int);
void ves_icall_RuntimeType_MakeGenericType_raw (int,int,int,int);
int ves_icall_RuntimeType_GetMethodsByName_native_raw (int,int,int,int,int);
int ves_icall_RuntimeType_GetPropertiesByName_native_raw (int,int,int,int,int);
int ves_icall_RuntimeType_GetConstructors_native_raw (int,int,int);
void ves_icall_RuntimeType_GetInterfaceMapData_raw (int,int,int,int,int);
int ves_icall_System_RuntimeType_CreateInstanceInternal_raw (int,int);
void ves_icall_RuntimeType_GetDeclaringMethod_raw (int,int,int);
void ves_icall_System_RuntimeType_getFullName_raw (int,int,int,int,int);
void ves_icall_RuntimeType_GetGenericArgumentsInternal_raw (int,int,int,int);
int ves_icall_RuntimeType_GetGenericParameterPosition (int);
int ves_icall_RuntimeType_GetEvents_native_raw (int,int,int,int);
int ves_icall_RuntimeType_GetFields_native_raw (int,int,int,int,int);
void ves_icall_RuntimeType_GetInterfaces_raw (int,int,int);
int ves_icall_RuntimeType_GetNestedTypes_native_raw (int,int,int,int,int);
void ves_icall_RuntimeType_GetDeclaringType_raw (int,int,int);
void ves_icall_RuntimeType_GetName_raw (int,int,int);
void ves_icall_RuntimeType_GetNamespace_raw (int,int,int);
int ves_icall_RuntimeType_FunctionPointerReturnAndParameterTypes_raw (int,int);
int ves_icall_RuntimeTypeHandle_GetAttributes (int);
int ves_icall_RuntimeTypeHandle_GetMetadataToken_raw (int,int);
void ves_icall_RuntimeTypeHandle_GetGenericTypeDefinition_impl_raw (int,int,int);
int ves_icall_RuntimeTypeHandle_GetCorElementType (int);
int ves_icall_RuntimeTypeHandle_HasInstantiation (int);
int ves_icall_RuntimeTypeHandle_IsInstanceOfType_raw (int,int,int);
int ves_icall_RuntimeTypeHandle_HasReferences_raw (int,int);
int ves_icall_RuntimeTypeHandle_GetArrayRank_raw (int,int);
void ves_icall_RuntimeTypeHandle_GetAssembly_raw (int,int,int);
void ves_icall_RuntimeTypeHandle_GetElementType_raw (int,int,int);
void ves_icall_RuntimeTypeHandle_GetModule_raw (int,int,int);
void ves_icall_RuntimeTypeHandle_GetBaseType_raw (int,int,int);
int ves_icall_RuntimeTypeHandle_type_is_assignable_from_raw (int,int,int);
int ves_icall_RuntimeTypeHandle_IsGenericTypeDefinition (int);
int ves_icall_RuntimeTypeHandle_GetGenericParameterInfo_raw (int,int);
int ves_icall_RuntimeTypeHandle_is_subclass_of_raw (int,int,int);
int ves_icall_RuntimeTypeHandle_IsByRefLike_raw (int,int);
void ves_icall_System_RuntimeTypeHandle_internal_from_name_raw (int,int,int,int,int,int);
int ves_icall_System_String_FastAllocateString_raw (int,int);
int ves_icall_System_String_InternalIsInterned_raw (int,int);
int ves_icall_System_String_InternalIntern_raw (int,int);
int ves_icall_System_Type_internal_from_handle_raw (int,int);
int ves_icall_System_ValueType_InternalGetHashCode_raw (int,int,int);
int ves_icall_System_ValueType_Equals_raw (int,int,int,int);
int ves_icall_System_Threading_Interlocked_CompareExchange_Int (int,int,int);
void ves_icall_System_Threading_Interlocked_CompareExchange_Object (int,int,int,int);
int ves_icall_System_Threading_Interlocked_Decrement_Int (int);
int ves_icall_System_Threading_Interlocked_Increment_Int (int);
int64_t ves_icall_System_Threading_Interlocked_Increment_Long (int);
int ves_icall_System_Threading_Interlocked_Exchange_Int (int,int);
void ves_icall_System_Threading_Interlocked_Exchange_Object (int,int,int);
int64_t ves_icall_System_Threading_Interlocked_CompareExchange_Long (int,int64_t,int64_t);
int64_t ves_icall_System_Threading_Interlocked_Exchange_Long (int,int64_t);
int64_t ves_icall_System_Threading_Interlocked_Read_Long (int);
int ves_icall_System_Threading_Interlocked_Add_Int (int,int);
int64_t ves_icall_System_Threading_Interlocked_Add_Long (int,int64_t);
void ves_icall_System_Threading_Monitor_Monitor_Enter_raw (int,int);
void mono_monitor_exit_icall_raw (int,int);
void ves_icall_System_Threading_Monitor_Monitor_pulse_raw (int,int);
void ves_icall_System_Threading_Monitor_Monitor_pulse_all_raw (int,int);
int ves_icall_System_Threading_Monitor_Monitor_wait_raw (int,int,int,int);
void ves_icall_System_Threading_Monitor_Monitor_try_enter_with_atomic_var_raw (int,int,int,int,int);
void ves_icall_System_Threading_Thread_StartInternal_raw (int,int,int);
void ves_icall_System_Threading_Thread_InitInternal_raw (int,int);
int ves_icall_System_Threading_Thread_GetCurrentThread ();
void ves_icall_System_Threading_InternalThread_Thread_free_internal_raw (int,int);
int ves_icall_System_Threading_Thread_GetState_raw (int,int);
void ves_icall_System_Threading_Thread_SetState_raw (int,int,int);
void ves_icall_System_Threading_Thread_ClrState_raw (int,int,int);
void ves_icall_System_Threading_Thread_SetName_icall_raw (int,int,int,int);
int ves_icall_System_Threading_Thread_YieldInternal ();
void ves_icall_System_Threading_Thread_SetPriority_raw (int,int,int);
void ves_icall_System_Runtime_Loader_AssemblyLoadContext_PrepareForAssemblyLoadContextRelease_raw (int,int,int);
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_GetLoadContextForAssembly_raw (int,int);
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFile_raw (int,int,int,int);
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalInitializeNativeALC_raw (int,int,int,int,int);
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFromStream_raw (int,int,int,int,int,int);
int ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalGetLoadedAssemblies_raw (int);
int ves_icall_System_GCHandle_InternalAlloc_raw (int,int,int);
void ves_icall_System_GCHandle_InternalFree_raw (int,int);
int ves_icall_System_GCHandle_InternalGet_raw (int,int);
void ves_icall_System_GCHandle_InternalSet_raw (int,int,int);
int ves_icall_System_Runtime_InteropServices_Marshal_GetLastPInvokeError ();
void ves_icall_System_Runtime_InteropServices_Marshal_SetLastPInvokeError (int);
void ves_icall_System_Runtime_InteropServices_Marshal_StructureToPtr_raw (int,int,int,int);
int ves_icall_System_Runtime_InteropServices_NativeLibrary_LoadByName_raw (int,int,int,int,int,int);
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalGetHashCode_raw (int,int);
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetObjectValue_raw (int,int);
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetUninitializedObjectInternal_raw (int,int);
void ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray_raw (int,int,int);
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetSpanDataFrom_raw (int,int,int,int);
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SufficientExecutionStack ();
int ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalBox_raw (int,int,int);
int ves_icall_System_Reflection_Assembly_GetExecutingAssembly_raw (int,int);
int ves_icall_System_Reflection_Assembly_GetEntryAssembly_raw (int);
int ves_icall_System_Reflection_Assembly_InternalLoad_raw (int,int,int,int);
int ves_icall_System_Reflection_Assembly_InternalGetType_raw (int,int,int,int,int,int);
int ves_icall_System_Reflection_AssemblyName_GetNativeName (int);
int ves_icall_MonoCustomAttrs_GetCustomAttributesInternal_raw (int,int,int,int);
int ves_icall_MonoCustomAttrs_GetCustomAttributesDataInternal_raw (int,int);
int ves_icall_MonoCustomAttrs_IsDefinedInternal_raw (int,int,int);
int ves_icall_System_Reflection_FieldInfo_internal_from_handle_type_raw (int,int,int);
int ves_icall_System_Reflection_FieldInfo_get_marshal_info_raw (int,int);
int ves_icall_System_Reflection_LoaderAllocatorScout_Destroy (int);
void ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceNames_raw (int,int,int);
void ves_icall_System_Reflection_RuntimeAssembly_GetExportedTypes_raw (int,int,int);
void ves_icall_System_Reflection_RuntimeAssembly_GetInfo_raw (int,int,int,int);
int ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceInternal_raw (int,int,int,int,int);
void ves_icall_System_Reflection_Assembly_GetManifestModuleInternal_raw (int,int,int);
void ves_icall_System_Reflection_RuntimeAssembly_GetModulesInternal_raw (int,int,int);
void ves_icall_System_Reflection_RuntimeCustomAttributeData_ResolveArgumentsInternal_raw (int,int,int,int,int,int,int);
void ves_icall_RuntimeEventInfo_get_event_info_raw (int,int,int);
int ves_icall_reflection_get_token_raw (int,int);
int ves_icall_System_Reflection_EventInfo_internal_from_handle_type_raw (int,int,int);
int ves_icall_RuntimeFieldInfo_ResolveType_raw (int,int);
int ves_icall_RuntimeFieldInfo_GetParentType_raw (int,int,int);
int ves_icall_RuntimeFieldInfo_GetFieldOffset_raw (int,int);
int ves_icall_RuntimeFieldInfo_GetValueInternal_raw (int,int,int);
void ves_icall_RuntimeFieldInfo_SetValueInternal_raw (int,int,int,int);
int ves_icall_RuntimeFieldInfo_GetRawConstantValue_raw (int,int);
int ves_icall_reflection_get_token_raw (int,int);
void ves_icall_get_method_info_raw (int,int,int);
int ves_icall_get_method_attributes (int);
int ves_icall_System_Reflection_MonoMethodInfo_get_parameter_info_raw (int,int,int);
int ves_icall_System_MonoMethodInfo_get_retval_marshal_raw (int,int);
int ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodFromHandleInternalType_native_raw (int,int,int,int);
int ves_icall_RuntimeMethodInfo_get_name_raw (int,int);
int ves_icall_RuntimeMethodInfo_get_base_method_raw (int,int,int);
int ves_icall_reflection_get_token_raw (int,int);
int ves_icall_InternalInvoke_raw (int,int,int,int,int);
void ves_icall_RuntimeMethodInfo_GetPInvoke_raw (int,int,int,int,int);
int ves_icall_RuntimeMethodInfo_MakeGenericMethod_impl_raw (int,int,int);
int ves_icall_RuntimeMethodInfo_GetGenericArguments_raw (int,int);
int ves_icall_RuntimeMethodInfo_GetGenericMethodDefinition_raw (int,int);
int ves_icall_RuntimeMethodInfo_get_IsGenericMethodDefinition_raw (int,int);
int ves_icall_RuntimeMethodInfo_get_IsGenericMethod_raw (int,int);
void ves_icall_InvokeClassConstructor_raw (int,int);
int ves_icall_InternalInvoke_raw (int,int,int,int,int);
int ves_icall_reflection_get_token_raw (int,int);
int ves_icall_System_Reflection_RuntimeModule_InternalGetTypes_raw (int,int);
int ves_icall_System_Reflection_RuntimeModule_ResolveMethodToken_raw (int,int,int,int,int,int);
int ves_icall_RuntimeParameterInfo_GetTypeModifiers_raw (int,int,int,int,int,int);
void ves_icall_RuntimePropertyInfo_get_property_info_raw (int,int,int,int);
int ves_icall_reflection_get_token_raw (int,int);
int ves_icall_System_Reflection_RuntimePropertyInfo_internal_from_handle_type_raw (int,int,int);
void ves_icall_DynamicMethod_create_dynamic_method_raw (int,int,int,int,int);
void ves_icall_AssemblyBuilder_basic_init_raw (int,int);
void ves_icall_AssemblyBuilder_UpdateNativeCustomAttributes_raw (int,int);
void ves_icall_ModuleBuilder_basic_init_raw (int,int);
void ves_icall_ModuleBuilder_set_wrappers_type_raw (int,int,int);
int ves_icall_ModuleBuilder_getUSIndex_raw (int,int,int);
int ves_icall_ModuleBuilder_getToken_raw (int,int,int,int);
int ves_icall_ModuleBuilder_getMethodToken_raw (int,int,int,int);
void ves_icall_ModuleBuilder_RegisterToken_raw (int,int,int,int);
int ves_icall_TypeBuilder_create_runtime_class_raw (int,int);
int ves_icall_System_IO_Stream_HasOverriddenBeginEndRead_raw (int,int);
int ves_icall_System_IO_Stream_HasOverriddenBeginEndWrite_raw (int,int);
int ves_icall_System_Diagnostics_Debugger_IsAttached_internal ();
void ves_icall_System_Diagnostics_Debugger_Log (int,int,int);
int ves_icall_System_Diagnostics_StackFrame_GetFrameInfo (int,int,int,int,int,int,int,int);
void ves_icall_System_Diagnostics_StackTrace_GetTrace (int,int,int,int);
int ves_icall_Mono_RuntimeClassHandle_GetTypeFromClass (int);
void ves_icall_Mono_RuntimeGPtrArrayHandle_GPtrArrayFree (int);
int ves_icall_Mono_SafeStringMarshal_StringToUtf8 (int);
void ves_icall_Mono_SafeStringMarshal_GFree (int);
static void *corlib_icall_funcs [] = {
// token 186,
ves_icall_System_Array_InternalCreate,
// token 199,
ves_icall_System_Array_GetCorElementTypeOfElementTypeInternal,
// token 200,
ves_icall_System_Array_IsValueOfElementTypeInternal,
// token 201,
ves_icall_System_Array_CanChangePrimitive,
// token 202,
ves_icall_System_Array_FastCopy,
// token 203,
ves_icall_System_Array_GetLengthInternal_raw,
// token 204,
ves_icall_System_Array_GetLowerBoundInternal_raw,
// token 205,
ves_icall_System_Array_GetGenericValue_icall,
// token 206,
ves_icall_System_Array_GetValueImpl_raw,
// token 207,
ves_icall_System_Array_SetGenericValue_icall,
// token 210,
ves_icall_System_Array_SetValueImpl_raw,
// token 211,
ves_icall_System_Array_InitializeInternal_raw,
// token 212,
ves_icall_System_Array_SetValueRelaxedImpl_raw,
// token 385,
ves_icall_System_Runtime_RuntimeImports_ZeroMemory,
// token 386,
ves_icall_System_Runtime_RuntimeImports_Memmove,
// token 387,
ves_icall_System_Buffer_BulkMoveWithWriteBarrier,
// token 415,
ves_icall_System_Delegate_AllocDelegateLike_internal_raw,
// token 416,
ves_icall_System_Delegate_CreateDelegate_internal_raw,
// token 417,
ves_icall_System_Delegate_GetVirtualMethod_internal_raw,
// token 444,
ves_icall_System_Enum_GetEnumValuesAndNames_raw,
// token 445,
ves_icall_System_Enum_InternalGetCorElementType,
// token 446,
ves_icall_System_Enum_InternalGetUnderlyingType_raw,
// token 563,
ves_icall_System_Environment_get_ProcessorCount,
// token 564,
ves_icall_System_Environment_get_TickCount,
// token 565,
ves_icall_System_Environment_get_TickCount64,
// token 568,
ves_icall_System_Environment_FailFast_raw,
// token 604,
ves_icall_System_GC_register_ephemeron_array_raw,
// token 605,
ves_icall_System_GC_get_ephemeron_tombstone_raw,
// token 607,
ves_icall_System_GC_SuppressFinalize_raw,
// token 609,
ves_icall_System_GC_ReRegisterForFinalize_raw,
// token 611,
ves_icall_System_GC_GetGCMemoryInfo,
// token 613,
ves_icall_System_GC_AllocPinnedArray_raw,
// token 618,
ves_icall_System_Object_MemberwiseClone_raw,
// token 626,
ves_icall_System_Math_Acos,
// token 627,
ves_icall_System_Math_Acosh,
// token 628,
ves_icall_System_Math_Asin,
// token 629,
ves_icall_System_Math_Asinh,
// token 630,
ves_icall_System_Math_Atan,
// token 631,
ves_icall_System_Math_Atan2,
// token 632,
ves_icall_System_Math_Atanh,
// token 633,
ves_icall_System_Math_Cbrt,
// token 634,
ves_icall_System_Math_Ceiling,
// token 635,
ves_icall_System_Math_Cos,
// token 636,
ves_icall_System_Math_Cosh,
// token 637,
ves_icall_System_Math_Exp,
// token 638,
ves_icall_System_Math_Floor,
// token 639,
ves_icall_System_Math_Log,
// token 640,
ves_icall_System_Math_Log10,
// token 641,
ves_icall_System_Math_Pow,
// token 642,
ves_icall_System_Math_Sin,
// token 644,
ves_icall_System_Math_Sinh,
// token 645,
ves_icall_System_Math_Sqrt,
// token 646,
ves_icall_System_Math_Tan,
// token 647,
ves_icall_System_Math_Tanh,
// token 648,
ves_icall_System_Math_FusedMultiplyAdd,
// token 649,
ves_icall_System_Math_Log2,
// token 650,
ves_icall_System_Math_ModF,
// token 741,
ves_icall_System_MathF_Acos,
// token 742,
ves_icall_System_MathF_Acosh,
// token 743,
ves_icall_System_MathF_Asin,
// token 744,
ves_icall_System_MathF_Asinh,
// token 745,
ves_icall_System_MathF_Atan,
// token 746,
ves_icall_System_MathF_Atan2,
// token 747,
ves_icall_System_MathF_Atanh,
// token 748,
ves_icall_System_MathF_Cbrt,
// token 749,
ves_icall_System_MathF_Ceiling,
// token 750,
ves_icall_System_MathF_Cos,
// token 751,
ves_icall_System_MathF_Cosh,
// token 752,
ves_icall_System_MathF_Exp,
// token 753,
ves_icall_System_MathF_Floor,
// token 754,
ves_icall_System_MathF_Log,
// token 755,
ves_icall_System_MathF_Log10,
// token 756,
ves_icall_System_MathF_Pow,
// token 757,
ves_icall_System_MathF_Sin,
// token 759,
ves_icall_System_MathF_Sinh,
// token 760,
ves_icall_System_MathF_Sqrt,
// token 761,
ves_icall_System_MathF_Tan,
// token 762,
ves_icall_System_MathF_Tanh,
// token 763,
ves_icall_System_MathF_FusedMultiplyAdd,
// token 764,
ves_icall_System_MathF_Log2,
// token 765,
ves_icall_System_MathF_ModF,
// token 827,
ves_icall_RuntimeMethodHandle_GetFunctionPointer_raw,
// token 836,
ves_icall_RuntimeMethodHandle_ReboxFromNullable_raw,
// token 837,
ves_icall_RuntimeMethodHandle_ReboxToNullable_raw,
// token 907,
ves_icall_RuntimeType_GetCorrespondingInflatedMethod_raw,
// token 913,
ves_icall_RuntimeType_make_array_type_raw,
// token 916,
ves_icall_RuntimeType_make_byref_type_raw,
// token 918,
ves_icall_RuntimeType_make_pointer_type_raw,
// token 923,
ves_icall_RuntimeType_MakeGenericType_raw,
// token 924,
ves_icall_RuntimeType_GetMethodsByName_native_raw,
// token 926,
ves_icall_RuntimeType_GetPropertiesByName_native_raw,
// token 927,
ves_icall_RuntimeType_GetConstructors_native_raw,
// token 931,
ves_icall_RuntimeType_GetInterfaceMapData_raw,
// token 933,
ves_icall_System_RuntimeType_CreateInstanceInternal_raw,
// token 934,
ves_icall_RuntimeType_GetDeclaringMethod_raw,
// token 936,
ves_icall_System_RuntimeType_getFullName_raw,
// token 937,
ves_icall_RuntimeType_GetGenericArgumentsInternal_raw,
// token 940,
ves_icall_RuntimeType_GetGenericParameterPosition,
// token 941,
ves_icall_RuntimeType_GetEvents_native_raw,
// token 942,
ves_icall_RuntimeType_GetFields_native_raw,
// token 945,
ves_icall_RuntimeType_GetInterfaces_raw,
// token 947,
ves_icall_RuntimeType_GetNestedTypes_native_raw,
// token 950,
ves_icall_RuntimeType_GetDeclaringType_raw,
// token 952,
ves_icall_RuntimeType_GetName_raw,
// token 954,
ves_icall_RuntimeType_GetNamespace_raw,
// token 963,
ves_icall_RuntimeType_FunctionPointerReturnAndParameterTypes_raw,
// token 1030,
ves_icall_RuntimeTypeHandle_GetAttributes,
// token 1032,
ves_icall_RuntimeTypeHandle_GetMetadataToken_raw,
// token 1034,
ves_icall_RuntimeTypeHandle_GetGenericTypeDefinition_impl_raw,
// token 1044,
ves_icall_RuntimeTypeHandle_GetCorElementType,
// token 1045,
ves_icall_RuntimeTypeHandle_HasInstantiation,
// token 1046,
ves_icall_RuntimeTypeHandle_IsInstanceOfType_raw,
// token 1048,
ves_icall_RuntimeTypeHandle_HasReferences_raw,
// token 1054,
ves_icall_RuntimeTypeHandle_GetArrayRank_raw,
// token 1055,
ves_icall_RuntimeTypeHandle_GetAssembly_raw,
// token 1056,
ves_icall_RuntimeTypeHandle_GetElementType_raw,
// token 1057,
ves_icall_RuntimeTypeHandle_GetModule_raw,
// token 1058,
ves_icall_RuntimeTypeHandle_GetBaseType_raw,
// token 1066,
ves_icall_RuntimeTypeHandle_type_is_assignable_from_raw,
// token 1067,
ves_icall_RuntimeTypeHandle_IsGenericTypeDefinition,
// token 1068,
ves_icall_RuntimeTypeHandle_GetGenericParameterInfo_raw,
// token 1072,
ves_icall_RuntimeTypeHandle_is_subclass_of_raw,
// token 1073,
ves_icall_RuntimeTypeHandle_IsByRefLike_raw,
// token 1075,
ves_icall_System_RuntimeTypeHandle_internal_from_name_raw,
// token 1079,
ves_icall_System_String_FastAllocateString_raw,
// token 1080,
ves_icall_System_String_InternalIsInterned_raw,
// token 1081,
ves_icall_System_String_InternalIntern_raw,
// token 1373,
ves_icall_System_Type_internal_from_handle_raw,
// token 1566,
ves_icall_System_ValueType_InternalGetHashCode_raw,
// token 1567,
ves_icall_System_ValueType_Equals_raw,
// token 8373,
ves_icall_System_Threading_Interlocked_CompareExchange_Int,
// token 8374,
ves_icall_System_Threading_Interlocked_CompareExchange_Object,
// token 8376,
ves_icall_System_Threading_Interlocked_Decrement_Int,
// token 8377,
ves_icall_System_Threading_Interlocked_Increment_Int,
// token 8378,
ves_icall_System_Threading_Interlocked_Increment_Long,
// token 8379,
ves_icall_System_Threading_Interlocked_Exchange_Int,
// token 8380,
ves_icall_System_Threading_Interlocked_Exchange_Object,
// token 8382,
ves_icall_System_Threading_Interlocked_CompareExchange_Long,
// token 8383,
ves_icall_System_Threading_Interlocked_Exchange_Long,
// token 8384,
ves_icall_System_Threading_Interlocked_Read_Long,
// token 8385,
ves_icall_System_Threading_Interlocked_Add_Int,
// token 8386,
ves_icall_System_Threading_Interlocked_Add_Long,
// token 8404,
ves_icall_System_Threading_Monitor_Monitor_Enter_raw,
// token 8406,
mono_monitor_exit_icall_raw,
// token 8411,
ves_icall_System_Threading_Monitor_Monitor_pulse_raw,
// token 8413,
ves_icall_System_Threading_Monitor_Monitor_pulse_all_raw,
// token 8415,
ves_icall_System_Threading_Monitor_Monitor_wait_raw,
// token 8417,
ves_icall_System_Threading_Monitor_Monitor_try_enter_with_atomic_var_raw,
// token 8466,
ves_icall_System_Threading_Thread_StartInternal_raw,
// token 8472,
ves_icall_System_Threading_Thread_InitInternal_raw,
// token 8473,
ves_icall_System_Threading_Thread_GetCurrentThread,
// token 8475,
ves_icall_System_Threading_InternalThread_Thread_free_internal_raw,
// token 8476,
ves_icall_System_Threading_Thread_GetState_raw,
// token 8477,
ves_icall_System_Threading_Thread_SetState_raw,
// token 8478,
ves_icall_System_Threading_Thread_ClrState_raw,
// token 8479,
ves_icall_System_Threading_Thread_SetName_icall_raw,
// token 8481,
ves_icall_System_Threading_Thread_YieldInternal,
// token 8483,
ves_icall_System_Threading_Thread_SetPriority_raw,
// token 9659,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_PrepareForAssemblyLoadContextRelease_raw,
// token 9663,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_GetLoadContextForAssembly_raw,
// token 9665,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFile_raw,
// token 9666,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalInitializeNativeALC_raw,
// token 9667,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalLoadFromStream_raw,
// token 9668,
ves_icall_System_Runtime_Loader_AssemblyLoadContext_InternalGetLoadedAssemblies_raw,
// token 10098,
ves_icall_System_GCHandle_InternalAlloc_raw,
// token 10099,
ves_icall_System_GCHandle_InternalFree_raw,
// token 10100,
ves_icall_System_GCHandle_InternalGet_raw,
// token 10101,
ves_icall_System_GCHandle_InternalSet_raw,
// token 10121,
ves_icall_System_Runtime_InteropServices_Marshal_GetLastPInvokeError,
// token 10122,
ves_icall_System_Runtime_InteropServices_Marshal_SetLastPInvokeError,
// token 10123,
ves_icall_System_Runtime_InteropServices_Marshal_StructureToPtr_raw,
// token 10168,
ves_icall_System_Runtime_InteropServices_NativeLibrary_LoadByName_raw,
// token 10249,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalGetHashCode_raw,
// token 10252,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetObjectValue_raw,
// token 10260,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetUninitializedObjectInternal_raw,
// token 10261,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InitializeArray_raw,
// token 10262,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_GetSpanDataFrom_raw,
// token 10263,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_SufficientExecutionStack,
// token 10264,
ves_icall_System_Runtime_CompilerServices_RuntimeHelpers_InternalBox_raw,
// token 10583,
ves_icall_System_Reflection_Assembly_GetExecutingAssembly_raw,
// token 10584,
ves_icall_System_Reflection_Assembly_GetEntryAssembly_raw,
// token 10589,
ves_icall_System_Reflection_Assembly_InternalLoad_raw,
// token 10590,
ves_icall_System_Reflection_Assembly_InternalGetType_raw,
// token 10623,
ves_icall_System_Reflection_AssemblyName_GetNativeName,
// token 10659,
ves_icall_MonoCustomAttrs_GetCustomAttributesInternal_raw,
// token 10666,
ves_icall_MonoCustomAttrs_GetCustomAttributesDataInternal_raw,
// token 10673,
ves_icall_MonoCustomAttrs_IsDefinedInternal_raw,
// token 10684,
ves_icall_System_Reflection_FieldInfo_internal_from_handle_type_raw,
// token 10688,
ves_icall_System_Reflection_FieldInfo_get_marshal_info_raw,
// token 10713,
ves_icall_System_Reflection_LoaderAllocatorScout_Destroy,
// token 10797,
ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceNames_raw,
// token 10799,
ves_icall_System_Reflection_RuntimeAssembly_GetExportedTypes_raw,
// token 10809,
ves_icall_System_Reflection_RuntimeAssembly_GetInfo_raw,
// token 10811,
ves_icall_System_Reflection_RuntimeAssembly_GetManifestResourceInternal_raw,
// token 10812,
ves_icall_System_Reflection_Assembly_GetManifestModuleInternal_raw,
// token 10813,
ves_icall_System_Reflection_RuntimeAssembly_GetModulesInternal_raw,
// token 10820,
ves_icall_System_Reflection_RuntimeCustomAttributeData_ResolveArgumentsInternal_raw,
// token 10835,
ves_icall_RuntimeEventInfo_get_event_info_raw,
// token 10855,
ves_icall_reflection_get_token_raw,
// token 10856,
ves_icall_System_Reflection_EventInfo_internal_from_handle_type_raw,
// token 10864,
ves_icall_RuntimeFieldInfo_ResolveType_raw,
// token 10866,
ves_icall_RuntimeFieldInfo_GetParentType_raw,
// token 10873,
ves_icall_RuntimeFieldInfo_GetFieldOffset_raw,
// token 10874,
ves_icall_RuntimeFieldInfo_GetValueInternal_raw,
// token 10877,
ves_icall_RuntimeFieldInfo_SetValueInternal_raw,
// token 10879,
ves_icall_RuntimeFieldInfo_GetRawConstantValue_raw,
// token 10884,
ves_icall_reflection_get_token_raw,
// token 10890,
ves_icall_get_method_info_raw,
// token 10891,
ves_icall_get_method_attributes,
// token 10898,
ves_icall_System_Reflection_MonoMethodInfo_get_parameter_info_raw,
// token 10900,
ves_icall_System_MonoMethodInfo_get_retval_marshal_raw,
// token 10912,
ves_icall_System_Reflection_RuntimeMethodInfo_GetMethodFromHandleInternalType_native_raw,
// token 10915,
ves_icall_RuntimeMethodInfo_get_name_raw,
// token 10916,
ves_icall_RuntimeMethodInfo_get_base_method_raw,
// token 10917,
ves_icall_reflection_get_token_raw,
// token 10928,
ves_icall_InternalInvoke_raw,
// token 10938,
ves_icall_RuntimeMethodInfo_GetPInvoke_raw,
// token 10944,
ves_icall_RuntimeMethodInfo_MakeGenericMethod_impl_raw,
// token 10945,
ves_icall_RuntimeMethodInfo_GetGenericArguments_raw,
// token 10946,
ves_icall_RuntimeMethodInfo_GetGenericMethodDefinition_raw,
// token 10948,
ves_icall_RuntimeMethodInfo_get_IsGenericMethodDefinition_raw,
// token 10949,
ves_icall_RuntimeMethodInfo_get_IsGenericMethod_raw,
// token 10966,
ves_icall_InvokeClassConstructor_raw,
// token 10968,
ves_icall_InternalInvoke_raw,
// token 10983,
ves_icall_reflection_get_token_raw,
// token 11003,
ves_icall_System_Reflection_RuntimeModule_InternalGetTypes_raw,
// token 11004,
ves_icall_System_Reflection_RuntimeModule_ResolveMethodToken_raw,
// token 11029,
ves_icall_RuntimeParameterInfo_GetTypeModifiers_raw,
// token 11034,
ves_icall_RuntimePropertyInfo_get_property_info_raw,
// token 11064,
ves_icall_reflection_get_token_raw,
// token 11065,
ves_icall_System_Reflection_RuntimePropertyInfo_internal_from_handle_type_raw,
// token 11673,
ves_icall_DynamicMethod_create_dynamic_method_raw,
// token 11759,
ves_icall_AssemblyBuilder_basic_init_raw,
// token 11760,
ves_icall_AssemblyBuilder_UpdateNativeCustomAttributes_raw,
// token 11973,
ves_icall_ModuleBuilder_basic_init_raw,
// token 11974,
ves_icall_ModuleBuilder_set_wrappers_type_raw,
// token 11982,
ves_icall_ModuleBuilder_getUSIndex_raw,
// token 11983,
ves_icall_ModuleBuilder_getToken_raw,
// token 11984,
ves_icall_ModuleBuilder_getMethodToken_raw,
// token 11989,
ves_icall_ModuleBuilder_RegisterToken_raw,
// token 12045,
ves_icall_TypeBuilder_create_runtime_class_raw,
// token 12470,
ves_icall_System_IO_Stream_HasOverriddenBeginEndRead_raw,
// token 12471,
ves_icall_System_IO_Stream_HasOverriddenBeginEndWrite_raw,
// token 12693,
ves_icall_System_Diagnostics_Debugger_IsAttached_internal,
// token 12694,
ves_icall_System_Diagnostics_Debugger_Log,
// token 12700,
ves_icall_System_Diagnostics_StackFrame_GetFrameInfo,
// token 12710,
ves_icall_System_Diagnostics_StackTrace_GetTrace,
// token 14120,
ves_icall_Mono_RuntimeClassHandle_GetTypeFromClass,
// token 14141,
ves_icall_Mono_RuntimeGPtrArrayHandle_GPtrArrayFree,
// token 14143,
ves_icall_Mono_SafeStringMarshal_StringToUtf8,
// token 14145,
ves_icall_Mono_SafeStringMarshal_GFree,
};
static uint8_t corlib_icall_flags [] = {
0,
0,
0,
0,
0,
4,
4,
0,
4,
0,
4,
4,
4,
0,
0,
0,
4,
4,
4,
4,
0,
4,
0,
0,
0,
4,
4,
4,
4,
4,
0,
4,
4,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
0,
0,
4,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
0,
4,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
0,
0,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
0,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
4,
0,
0,
0,
0,
0,
0,
0,
0,
};
