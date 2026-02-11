// GENERATED FILE, DO NOT MODIFY");


void GlobalizationNative_ChangeCase (void *, int32_t, void *, int32_t, int32_t);

void GlobalizationNative_ChangeCaseInvariant (void *, int32_t, void *, int32_t, int32_t);

void GlobalizationNative_ChangeCaseTurkish (void *, int32_t, void *, int32_t, int32_t);

void GlobalizationNative_CloseSortHandle (void *);

int32_t GlobalizationNative_CompareString (void *, void *, int32_t, void *, int32_t, int32_t);

int32_t GlobalizationNative_EndsWith (void *, void *, int32_t, void *, int32_t, int32_t, void *);

int32_t GlobalizationNative_EnumCalendarInfo (void *, void *, uint32_t, int32_t, void *);

int32_t GlobalizationNative_GetCalendarInfo (void *, uint32_t, int32_t, void *, int32_t);

int32_t GlobalizationNative_GetCalendars (void *, void *, int32_t);

int32_t GlobalizationNative_GetDefaultLocaleName (void *, int32_t);

int32_t GlobalizationNative_GetJapaneseEraStartDate (int32_t, void *, void *, void *);

int32_t GlobalizationNative_GetLatestJapaneseEra ();

int32_t GlobalizationNative_GetLocaleInfoGroupingSizes (void *, uint32_t, void *, void *);

int32_t GlobalizationNative_GetLocaleInfoInt (void *, uint32_t, void *);

int32_t GlobalizationNative_GetLocaleInfoString (void *, uint32_t, void *, int32_t, void *);

int32_t GlobalizationNative_GetLocaleName (void *, void *, int32_t);

int32_t GlobalizationNative_GetLocaleTimeFormat (void *, int32_t, void *, int32_t);

int32_t GlobalizationNative_GetSortHandle (void *, void *);

int32_t GlobalizationNative_GetSortKey (void *, void *, int32_t, void *, int32_t, int32_t);

int32_t GlobalizationNative_IndexOf (void *, void *, int32_t, void *, int32_t, int32_t, void *);

void GlobalizationNative_InitICUFunctions (void *, void *, void *, void *);

void GlobalizationNative_InitOrdinalCasingPage (int32_t, void *);

int32_t GlobalizationNative_IsNormalized (int32_t, void *, int32_t);

int32_t GlobalizationNative_IsPredefinedLocale (void *);

int32_t GlobalizationNative_LastIndexOf (void *, void *, int32_t, void *, int32_t, int32_t, void *);

int32_t GlobalizationNative_LoadICU ();

int32_t GlobalizationNative_NormalizeString (int32_t, void *, int32_t, void *, int32_t);

int32_t GlobalizationNative_StartsWith (void *, void *, int32_t, void *, int32_t, int32_t, void *);

int32_t GlobalizationNative_ToAscii (uint32_t, void *, int32_t, void *, int32_t);

int32_t GlobalizationNative_ToUnicode (uint32_t, void *, int32_t, void *, int32_t);

int32_t SystemNative_Close (void *);

int32_t SystemNative_CloseDir (void *);

int32_t SystemNative_ConvertErrorPalToPlatform (int32_t);

int32_t SystemNative_ConvertErrorPlatformToPal (int32_t);

void * SystemNative_Dup (void *);

int32_t SystemNative_FAllocate (void *, int64_t, int64_t);

int32_t SystemNative_FLock (void *, int32_t);

void SystemNative_Free (void *);

int32_t SystemNative_FStat (void *, void *);

int32_t SystemNative_FTruncate (void *, int64_t);

int32_t SystemNative_GetCryptographicallySecureRandomBytes (void *, int32_t);

void * SystemNative_GetCwd (void *, int32_t);

void * SystemNative_GetEnv (void *);

int32_t SystemNative_GetErrNo ();

uint32_t SystemNative_GetFileSystemType (void *);

void SystemNative_GetNonCryptographicallySecureRandomBytes (void *, int32_t);

int32_t SystemNative_GetReadDirRBufferSize ();

int64_t SystemNative_GetSystemTimeAsTicks ();

uint64_t SystemNative_GetTimestamp ();

void * SystemNative_GetTimeZoneData (void *, void *);

void SystemNative_LowLevelMonitor_Acquire (void *);

void * SystemNative_LowLevelMonitor_Create ();

void SystemNative_LowLevelMonitor_Destroy (void *);

void SystemNative_LowLevelMonitor_Release (void *);

void SystemNative_LowLevelMonitor_Signal_Release (void *);

int32_t SystemNative_LowLevelMonitor_TimedWait (void *, int32_t);

void SystemNative_LowLevelMonitor_Wait (void *);

int64_t SystemNative_LSeek (void *, int64_t, int32_t);

int32_t SystemNative_LStat (void *, void *);

void * SystemNative_Malloc (void *);

void * SystemNative_Open (void *, int32_t, int32_t);

void * SystemNative_OpenDir (void *);

int32_t SystemNative_PosixFAdvise (void *, int64_t, int64_t, int32_t);

int32_t SystemNative_PRead (void *, void *, int32_t, int64_t);

int32_t SystemNative_Read (void *, void *, int32_t);

int32_t SystemNative_ReadDirR (void *, void *, int32_t, void *);

int32_t SystemNative_ReadLink (void *, void *, int32_t);

int32_t SystemNative_SchedGetCpu ();

void SystemNative_SetErrNo (int32_t);

int32_t SystemNative_Stat (void *, void *);

void * SystemNative_StrErrorR (int32_t, void *, int32_t);

uint32_t SystemNative_TryGetUInt32OSThreadId ();

int32_t SystemNative_Unlink (void *);

int32_t SystemNative_Write (void *, void *, int32_t);
static PinvokeImport libSystem_Native_imports [] = {
    {"SystemNative_Close", SystemNative_Close}, // System.Private.CoreLib
    {"SystemNative_CloseDir", SystemNative_CloseDir}, // System.Private.CoreLib
    {"SystemNative_ConvertErrorPalToPlatform", SystemNative_ConvertErrorPalToPlatform}, // System.Console, System.Net.Primitives, System.Private.CoreLib
    {"SystemNative_ConvertErrorPlatformToPal", SystemNative_ConvertErrorPlatformToPal}, // System.Console, System.Private.CoreLib
    {"SystemNative_Dup", SystemNative_Dup}, // System.Console
    {"SystemNative_FAllocate", SystemNative_FAllocate}, // System.Private.CoreLib
    {"SystemNative_FLock", SystemNative_FLock}, // System.Private.CoreLib
    {"SystemNative_Free", SystemNative_Free}, // System.Private.CoreLib
    {"SystemNative_FStat", SystemNative_FStat}, // System.Private.CoreLib
    {"SystemNative_FTruncate", SystemNative_FTruncate}, // System.Private.CoreLib
    {"SystemNative_GetCryptographicallySecureRandomBytes", SystemNative_GetCryptographicallySecureRandomBytes}, // System.Private.CoreLib
    {"SystemNative_GetCwd", SystemNative_GetCwd}, // System.Private.CoreLib
    {"SystemNative_GetEnv", SystemNative_GetEnv}, // System.Private.CoreLib
    {"SystemNative_GetErrNo", SystemNative_GetErrNo}, // System.Private.CoreLib
    {"SystemNative_GetFileSystemType", SystemNative_GetFileSystemType}, // System.Private.CoreLib
    {"SystemNative_GetNonCryptographicallySecureRandomBytes", SystemNative_GetNonCryptographicallySecureRandomBytes}, // System.Private.CoreLib
    {"SystemNative_GetReadDirRBufferSize", SystemNative_GetReadDirRBufferSize}, // System.Private.CoreLib
    {"SystemNative_GetSystemTimeAsTicks", SystemNative_GetSystemTimeAsTicks}, // System.Private.CoreLib
    {"SystemNative_GetTimestamp", SystemNative_GetTimestamp}, // System.Private.CoreLib
    {"SystemNative_GetTimeZoneData", SystemNative_GetTimeZoneData}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Acquire", SystemNative_LowLevelMonitor_Acquire}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Create", SystemNative_LowLevelMonitor_Create}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Destroy", SystemNative_LowLevelMonitor_Destroy}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Release", SystemNative_LowLevelMonitor_Release}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Signal_Release", SystemNative_LowLevelMonitor_Signal_Release}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_TimedWait", SystemNative_LowLevelMonitor_TimedWait}, // System.Private.CoreLib
    {"SystemNative_LowLevelMonitor_Wait", SystemNative_LowLevelMonitor_Wait}, // System.Private.CoreLib
    {"SystemNative_LSeek", SystemNative_LSeek}, // System.Private.CoreLib
    {"SystemNative_LStat", SystemNative_LStat}, // System.Private.CoreLib
    {"SystemNative_Malloc", SystemNative_Malloc}, // System.Private.CoreLib
    {"SystemNative_Open", SystemNative_Open}, // System.Private.CoreLib
    {"SystemNative_OpenDir", SystemNative_OpenDir}, // System.Private.CoreLib
    {"SystemNative_PosixFAdvise", SystemNative_PosixFAdvise}, // System.Private.CoreLib
    {"SystemNative_PRead", SystemNative_PRead}, // System.Private.CoreLib
    {"SystemNative_Read", SystemNative_Read}, // System.Private.CoreLib
    {"SystemNative_ReadDirR", SystemNative_ReadDirR}, // System.Private.CoreLib
    {"SystemNative_ReadLink", SystemNative_ReadLink}, // System.Private.CoreLib
    {"SystemNative_SchedGetCpu", SystemNative_SchedGetCpu}, // System.Private.CoreLib
    {"SystemNative_SetErrNo", SystemNative_SetErrNo}, // System.Private.CoreLib
    {"SystemNative_Stat", SystemNative_Stat}, // System.Private.CoreLib
    {"SystemNative_StrErrorR", SystemNative_StrErrorR}, // System.Console, System.Net.Primitives, System.Private.CoreLib
    {"SystemNative_TryGetUInt32OSThreadId", SystemNative_TryGetUInt32OSThreadId}, // System.Private.CoreLib
    {"SystemNative_Unlink", SystemNative_Unlink}, // System.Private.CoreLib
    {"SystemNative_Write", SystemNative_Write}, // System.Console
    {NULL, NULL}
};
static PinvokeImport libSystem_IO_Compression_Native_imports [] = {
    {NULL, NULL}
};
static PinvokeImport libSystem_Globalization_Native_imports [] = {
    {"GlobalizationNative_ChangeCase", GlobalizationNative_ChangeCase}, // System.Private.CoreLib
    {"GlobalizationNative_ChangeCaseInvariant", GlobalizationNative_ChangeCaseInvariant}, // System.Private.CoreLib
    {"GlobalizationNative_ChangeCaseTurkish", GlobalizationNative_ChangeCaseTurkish}, // System.Private.CoreLib
    {"GlobalizationNative_CloseSortHandle", GlobalizationNative_CloseSortHandle}, // System.Private.CoreLib
    {"GlobalizationNative_CompareString", GlobalizationNative_CompareString}, // System.Private.CoreLib
    {"GlobalizationNative_EndsWith", GlobalizationNative_EndsWith}, // System.Private.CoreLib
    {"GlobalizationNative_EnumCalendarInfo", GlobalizationNative_EnumCalendarInfo}, // System.Private.CoreLib
    {"GlobalizationNative_GetCalendarInfo", GlobalizationNative_GetCalendarInfo}, // System.Private.CoreLib
    {"GlobalizationNative_GetCalendars", GlobalizationNative_GetCalendars}, // System.Private.CoreLib
    {"GlobalizationNative_GetDefaultLocaleName", GlobalizationNative_GetDefaultLocaleName}, // System.Private.CoreLib
    {"GlobalizationNative_GetJapaneseEraStartDate", GlobalizationNative_GetJapaneseEraStartDate}, // System.Private.CoreLib
    {"GlobalizationNative_GetLatestJapaneseEra", GlobalizationNative_GetLatestJapaneseEra}, // System.Private.CoreLib
    {"GlobalizationNative_GetLocaleInfoGroupingSizes", GlobalizationNative_GetLocaleInfoGroupingSizes}, // System.Private.CoreLib
    {"GlobalizationNative_GetLocaleInfoInt", GlobalizationNative_GetLocaleInfoInt}, // System.Private.CoreLib
    {"GlobalizationNative_GetLocaleInfoString", GlobalizationNative_GetLocaleInfoString}, // System.Private.CoreLib
    {"GlobalizationNative_GetLocaleName", GlobalizationNative_GetLocaleName}, // System.Private.CoreLib
    {"GlobalizationNative_GetLocaleTimeFormat", GlobalizationNative_GetLocaleTimeFormat}, // System.Private.CoreLib
    {"GlobalizationNative_GetSortHandle", GlobalizationNative_GetSortHandle}, // System.Private.CoreLib
    {"GlobalizationNative_GetSortKey", GlobalizationNative_GetSortKey}, // System.Private.CoreLib
    {"GlobalizationNative_IndexOf", GlobalizationNative_IndexOf}, // System.Private.CoreLib
    {"GlobalizationNative_InitICUFunctions", GlobalizationNative_InitICUFunctions}, // System.Private.CoreLib
    {"GlobalizationNative_InitOrdinalCasingPage", GlobalizationNative_InitOrdinalCasingPage}, // System.Private.CoreLib
    {"GlobalizationNative_IsNormalized", GlobalizationNative_IsNormalized}, // System.Private.CoreLib
    {"GlobalizationNative_IsPredefinedLocale", GlobalizationNative_IsPredefinedLocale}, // System.Private.CoreLib
    {"GlobalizationNative_LastIndexOf", GlobalizationNative_LastIndexOf}, // System.Private.CoreLib
    {"GlobalizationNative_LoadICU", GlobalizationNative_LoadICU}, // System.Private.CoreLib
    {"GlobalizationNative_NormalizeString", GlobalizationNative_NormalizeString}, // System.Private.CoreLib
    {"GlobalizationNative_StartsWith", GlobalizationNative_StartsWith}, // System.Private.CoreLib
    {"GlobalizationNative_ToAscii", GlobalizationNative_ToAscii}, // System.Private.CoreLib
    {"GlobalizationNative_ToUnicode", GlobalizationNative_ToUnicode}, // System.Private.CoreLib
    {NULL, NULL}
};

static void *pinvoke_tables[] = {
    (void*)libSystem_Native_imports, (void*)libSystem_IO_Compression_Native_imports, (void*)libSystem_Globalization_Native_imports
};

static char *pinvoke_names[] =  {
    "libSystem.Native", "libSystem.IO.Compression.Native", "libSystem.Globalization.Native"
};
#include <mono/utils/details/mono-error-types.h>
                #include <mono/metadata/assembly.h>
                #include <mono/utils/mono-error.h>
                #include <mono/metadata/object.h>
                #include <mono/utils/details/mono-logger-types.h>
                #include "runtime.h"
                InterpFtnDesc wasm_native_to_interp_ftndescs[4] = {};
typedef void (*WasmInterpEntrySig_0) (int*, int*, int*, int*, int*, int*, int*, int*);
int32_t wasm_native_to_interp_Internal_Runtime_InteropServices_System_Private_CoreLib_ComponentActivator_GetFunctionPointer (void * arg0, void * arg1, void * arg2, void * arg3, void * arg4, void * arg5) { 
  int32_t res;
  if (!(WasmInterpEntrySig_0)wasm_native_to_interp_ftndescs [0].func) {
   mono_wasm_marshal_get_managed_wrapper ("System.Private.CoreLib","Internal.Runtime.InteropServices", "ComponentActivator", "GetFunctionPointer", 6);
  }
  ((WasmInterpEntrySig_0)wasm_native_to_interp_ftndescs [0].func) ((int*)&res, (int*)&arg0, (int*)&arg1, (int*)&arg2, (int*)&arg3, (int*)&arg4, (int*)&arg5, wasm_native_to_interp_ftndescs [0].arg);
  return res;
}

typedef void (*WasmInterpEntrySig_1) (int*, int*, int*);
void wasm_native_to_interp_System_Globalization_System_Private_CoreLib_CalendarData_EnumCalendarInfoCallback (void * arg0, void * arg1) { 
  if (!(WasmInterpEntrySig_1)wasm_native_to_interp_ftndescs [1].func) {
   mono_wasm_marshal_get_managed_wrapper ("System.Private.CoreLib","System.Globalization", "CalendarData", "EnumCalendarInfoCallback", 2);
  }
  ((WasmInterpEntrySig_1)wasm_native_to_interp_ftndescs [1].func) ((int*)&arg0, (int*)&arg1, wasm_native_to_interp_ftndescs [1].arg);
}

typedef void (*WasmInterpEntrySig_2) (int*);
void wasm_native_to_interp_System_Threading_System_Private_CoreLib_ThreadPool_BackgroundJobHandler () { 
  if (!(WasmInterpEntrySig_2)wasm_native_to_interp_ftndescs [2].func) {
   mono_wasm_marshal_get_managed_wrapper ("System.Private.CoreLib","System.Threading", "ThreadPool", "BackgroundJobHandler", 0);
  }
  ((WasmInterpEntrySig_2)wasm_native_to_interp_ftndescs [2].func) (wasm_native_to_interp_ftndescs [2].arg);
}

typedef void (*WasmInterpEntrySig_3) (int*);
void wasm_native_to_interp_System_Threading_System_Private_CoreLib_TimerQueue_TimerHandler () { 
  if (!(WasmInterpEntrySig_3)wasm_native_to_interp_ftndescs [3].func) {
   mono_wasm_marshal_get_managed_wrapper ("System.Private.CoreLib","System.Threading", "TimerQueue", "TimerHandler", 0);
  }
  ((WasmInterpEntrySig_3)wasm_native_to_interp_ftndescs [3].func) (wasm_native_to_interp_ftndescs [3].arg);
}


static void *wasm_native_to_interp_funcs[] = {
    wasm_native_to_interp_Internal_Runtime_InteropServices_System_Private_CoreLib_ComponentActivator_GetFunctionPointer, wasm_native_to_interp_System_Globalization_System_Private_CoreLib_CalendarData_EnumCalendarInfoCallback, wasm_native_to_interp_System_Threading_System_Private_CoreLib_ThreadPool_BackgroundJobHandler, wasm_native_to_interp_System_Threading_System_Private_CoreLib_TimerQueue_TimerHandler
};

// these strings need to match the keys generated in get_native_to_interp
static const char *wasm_native_to_interp_map[] = {
    "System_Private_CoreLib_ComponentActivator_GetFunctionPointer", "System_Private_CoreLib_CalendarData_EnumCalendarInfoCallback", "System_Private_CoreLib_ThreadPool_BackgroundJobHandler", "System_Private_CoreLib_TimerQueue_TimerHandler"
};
