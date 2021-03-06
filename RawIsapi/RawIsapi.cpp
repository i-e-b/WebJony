// RawIsapi.cpp : Defines the exported functions for the DLL application.
//

// System bits
#include "stdafx.h"
#include <stdio.h>
#include <stdlib.h>

// Our stuff
#include "SharedMem.h"

// IIS requirements
#include <httpext.h>
#include <tchar.h>

// .Net requirements
#include <windows.h>
#include <metahost.h>
#include <corerror.h>
#pragma comment(lib, "mscoree.lib")

// Local function headers
void OutputToClient(EXTENSION_CONTROL_BLOCK *pEcb, const char* controlString, ...);
void OutputToClientW(EXTENSION_CONTROL_BLOCK *pEcb, PCWSTR str);
void ShutDownDotnet();
HRESULT EnsureInitialSetup(EXTENSION_CONTROL_BLOCK *pEcb, PCWSTR dllPath);
void SendHTMLHeader(EXTENSION_CONTROL_BLOCK *pEcb);
HRESULT StartDotnet(PCWSTR pszVersion, EXTENSION_CONTROL_BLOCK *pEcb);
HRESULT BuildFunctionTables(PCWSTR pszAssemblyPath, PCWSTR pszClassName, PCWSTR pszFunctionName, EXTENSION_CONTROL_BLOCK *pEcb);
const char* seDescription(DWORD code);

// Globals for .Net environment
ICLRMetaHost *pMetaHost = NULL;
ICLRRuntimeInfo *pRuntimeInfo = NULL;
ICLRRuntimeHost *pClrRuntimeHost = NULL; // ICLRRuntimeHost does not support loading the .NET v1.x runtimes.

// Function pointer types from the managed side (these must exactly match the .Net side)
typedef int(*INT_INT_FUNC_PTR)(int);
typedef void(*VOID_FUNC_PTR)();
typedef void(*STR_STR_FUNC_PTR)(PCWSTR, BSTR*);
typedef void(*ECB_FUNC_PTR)(HCONN, LPSTR, LPSTR, LPSTR, LPSTR, LPSTR, __int32, __int32, LPBYTE, void*, void*, void*, void*);


// Globals for .Net function pointers
STR_STR_FUNC_PTR dnWakeup;     // dot net initial wake up. We pass in the host site directory
VOID_FUNC_PTR    dnShutdown;   // dot net clean-up side
ECB_FUNC_PTR     dnHandleRq;   // Core ECB handler -- this is how we handle IIS requests in .Net, where the strings are easier.


// This flag is set if the .Net runtime fails to start, or we can't load the Host dll.
// Once set, the subsequent HTTP request will output diagnostics.
int SysStartupFailure = 0;

//========== IIS Interaction functions ==========

// Send back a IIS version compatibility number and a string, which seems to have no purpose
BOOL WINAPI GetExtensionVersion(HSE_VERSION_INFO *pVer)
{
    pVer->dwExtensionVersion = MAKELONG(HSE_VERSION_MINOR, HSE_VERSION_MAJOR);
    const char *name = "WebJony Host";
    int i = 0;
    do { pVer->lpszExtensionDesc[i] = name[i]; i++; } while (name[i] != 0);
    return TRUE;
}

// Handle a request
DWORD WINAPI HttpExtensionProc(EXTENSION_CONTROL_BLOCK *pEcb)
{
    EXTENSION_CONTROL_BLOCK *diagnosticOut = NULL;

    if (SysStartupFailure) {
        diagnosticOut = pEcb;
        SendHTMLHeader(pEcb);
        OutputToClient(pEcb, "Attempting to load ");
        OutputToClientW(pEcb, (Global_DllFilePath + 4)); // \\?\C:\Gits\RawIsapi\x64\Debug\RawIsapi.dll
        OutputToClient(pEcb, "\r\n");
    }
    
    HRESULT hr = EnsureInitialSetup(diagnosticOut, (Global_DllFilePath + 4)); // The `+4` knocks the `\\?\` off the dll path

    SysStartupFailure = hr;
    if (FAILED(hr)) {
        // if you get 0xC0000005 -- your path is probably wrong
        //            0x80070002 -- your path is probably wrong
        //            0x80131040 -- assembly name mismatch
        //            0x80131522 -- the assembly type or method name was not found
        //            0x80131023 -- CLR not available. Something went wrong in the initial set-up.
        //            0xE0434352 -- unhandled exception on CLR side
        SendHTMLHeader(pEcb);
        OutputToClient(pEcb, "\r\nSetup failed. Request again for diagnostics.");
        char buffer[1024];
        sprintf_s(buffer, "\r\nCode 0x%X", hr);
        OutputToClient(pEcb, buffer);

        ShutDownDotnet();
        return HSE_STATUS_SUCCESS;
    }
    DWORD code = 0;
    LPEXCEPTION_POINTERS info;

    __try {
        // Send the request to .Net:
        dnHandleRq(pEcb->ConnID,
            pEcb->lpszMethod, pEcb->lpszQueryString, pEcb->lpszPathInfo, pEcb->lpszPathTranslated, pEcb->lpszContentType,
            pEcb->cbTotalBytes, pEcb->cbAvailable, pEcb->lpbData,
            pEcb->GetServerVariable, pEcb->WriteClient, pEcb->ReadClient, pEcb->ServerSupportFunction);
    }
    __except (code = GetExceptionCode(), info = GetExceptionInformation(), EXCEPTION_EXECUTE_HANDLER) { }    

    if (code != 0) { // outside of handler to make things easier?
        SendHTMLHeader(pEcb);
        OutputToClient(pEcb, "Call to .Net failed\r\n");

        if (code == EXCEPTION_ACCESS_VIOLATION) {
            OutputToClient(pEcb, "Access violation\r\n");
        }
 
        OutputToClient(pEcb, seDescription(code));

        char buffer[1024];
        sprintf_s(buffer, "\r\nCode 0x%X", code);
        //  0x80131418  means a permissions issue
        //  0xE0434352  means the CLR fell over. Attach a debugger, or turn off all error catching and look in the Windows event log
        OutputToClient(pEcb, buffer);
    }

    return HSE_STATUS_SUCCESS;
}

// Clean-up on shut-down (app-pool recycle?)
BOOL WINAPI TerminateExtension(DWORD dwFlags)
{
    ShutDownDotnet();
    return TRUE;
}

//========== Internal functions ==========

LONG WINAPI MyUnhandledExceptionFilter(struct _EXCEPTION_POINTERS *exceptionInfo)
{
    // do something useful
    return EXCEPTION_EXECUTE_HANDLER; // prevent crash
}

bool setupDone = false;
HRESULT EnsureInitialSetup(EXTENSION_CONTROL_BLOCK *pEcb, PCWSTR dllPath)
{
    if (setupDone) return S_OK;
    setupDone = true;
    
    // Uncomment for production. Deactivate to get stack traces in the Event Viewer
    SetUnhandledExceptionFilter(MyUnhandledExceptionFilter);

    HRESULT hr;
    // Initial setup checks
    hr = StartDotnet(DOTNET_RUNTIME_VERSION, pEcb);
    if (FAILED(hr)) {
        return hr;
    }
    // TODO: Assume the communicator is in the same location as the RawIsapi dll? Read from web.config?
    hr = BuildFunctionTables(dllPath, DOTNET_ENTRY_CLASS_NAME, DOTNET_PROBING_STATIC, pEcb);
    if (FAILED(hr)) {
        return hr;
    }

    BSTR err;
    dnWakeup(dllPath, &err);

    return S_OK;
}

// Send shutdown signal to managed side, then close down the runtime.
void ShutDownDotnet() {
    if (dnShutdown) {
        dnShutdown();
    }
    if (pMetaHost)
    {
        pMetaHost->Release();
        pMetaHost = NULL;
    }
    if (pRuntimeInfo)
    {
        pRuntimeInfo->Release();
        pRuntimeInfo = NULL;
    }
    if (pClrRuntimeHost)
    {
        // Please note that after a call to Stop, the CLR cannot be 
        // reinitialized into the same process. This step is usually not 
        // necessary. You can leave the .NET runtime loaded in your process.
        //pClrRuntimeHost->Stop();
        pClrRuntimeHost->Release();
        pClrRuntimeHost = NULL;
    }
}

// Write a string out to a http request
void OutputToClient(EXTENSION_CONTROL_BLOCK *pEcb, const char* ctrlStr, ...)
{
    if (pEcb == NULL) return;
    DWORD buffsize;
    buffsize = (DWORD)strlen(ctrlStr);
    pEcb->WriteClient(pEcb->ConnID, (void*)ctrlStr, &buffsize, NULL);
}

void OutputToClientW(EXTENSION_CONTROL_BLOCK *pEcb, PCWSTR str)
{
    int len = WideCharToMultiByte(CP_ACP, 0, str, (int)wcslen(str), NULL, 0, NULL, NULL);
    char* buffer = new char[len + 1];
    WideCharToMultiByte(CP_ACP, 0, str, (int)wcslen(str), buffer, len, NULL, NULL);
    buffer[len] = '\0';

    OutputToClient(pEcb, buffer);
}

// Write *minimum* headers (only used in error conditions)
void SendHTMLHeader(EXTENSION_CONTROL_BLOCK *pEcb)
{
    char MyHeader[4096];
    strcpy_s(MyHeader, "Content-type: text/plain\n\n");
    pEcb->ServerSupportFunction(pEcb->ConnID, HSE_REQ_SEND_RESPONSE_HEADER, NULL, 0, (DWORD *)MyHeader);
}

// Call the .Net side to request a named function pointer
// pszClassName:         Namespace and name of class containing the callback function
// pszFunctionName:      Function that provides callbacks. This *must* be of the form public static int MyFunc(string functionName)
// ManagedFunctionName:  The .Net function we are trying to look up
// pEcb:                 (Optional) http session to write logs to
// result:               Output of function pointer, if successful
HRESULT FindFunction(PCWSTR pszAssemblyPath, PCWSTR pszClassName, PCWSTR pszFunctionName, PCWSTR ManagedFunctionName, EXTENSION_CONTROL_BLOCK *pEcb, ULONGLONG* result)
{
    HRESULT hr;
    // The invoked method of ExecuteInDefaultAppDomain must have the 
    // following signature: static int pwzMethodName (String pwzArgument)
    // where pwzMethodName represents the name of the invoked method, and 
    // pwzArgument represents the string value passed as a parameter to that 
    // method. If the HRESULT return value of ExecuteInDefaultAppDomain is 
    // set to S_OK, pReturnValue is set to the integer value returned by the 
    // invoked method. Otherwise, pReturnValue is not set.

    DWORD dwResult;

    hr = pClrRuntimeHost->ExecuteInDefaultAppDomain(pszAssemblyPath, pszClassName, pszFunctionName, ManagedFunctionName, &dwResult);
    if (FAILED(hr))
    {
        char buffer[1024];
        sprintf_s(buffer, "%X", hr);
        OutputToClient(pEcb, "\r\nFailed to call method.");
        switch (hr) {
        case HOST_E_CLRNOTAVAILABLE:
            OutputToClient(pEcb, "\r\nHOST_E_CLRNOTAVAILABLE");
            break;
        case HOST_E_TIMEOUT:
            OutputToClient(pEcb, "\r\nHOST_E_TIMEOUT");
            break;
        case HOST_E_NOT_OWNER:
            OutputToClient(pEcb, "\r\nHOST_E_NOT_OWNER");
            break;
        case HOST_E_ABANDONED:
            OutputToClient(pEcb, "\r\nHOST_E_ABANDONED");
            break;
        case E_FAIL:
            OutputToClient(pEcb, "\r\nE_FAIL - Non-standard error");
            break;
        case COR_E_FILENOTFOUND:
            OutputToClient(pEcb, "\r\nCOR_E_FILENOTFOUND - Check path and permissions");
            break;
        case COR_E_FILELOAD:
            OutputToClient(pEcb, "\r\nCOR_E_FILELOAD - Check path and permissions");
            break;
        case COR_E_ENDOFSTREAM:
            OutputToClient(pEcb, "\r\nCOR_E_ENDOFSTREAM - Check path and permissions");
            break;
        case COR_E_DIRECTORYNOTFOUND:
            OutputToClient(pEcb, "\r\nCOR_E_DIRECTORYNOTFOUND - Check path and permissions");
            break;
        case COR_E_PATHTOOLONG:
            OutputToClient(pEcb, "\r\nCOR_E_PATHTOOLONG - Check path and permissions");
            break;
        case COR_E_IO:
            OutputToClient(pEcb, "\r\nCOR_E_IO - Check path and permissions");
            break;
        case COR_E_OVERFLOW:
            OutputToClient(pEcb, "\r\nCOR_E_OVERFLOW - Some kind of casting or conversion error (likely on the .Net side -- check 32/64 bit setup)");
            // https://blogs.msdn.microsoft.com/yizhang/2010/12/17/interpreting-hresults-returned-from-netclr-0x8013xxxx/
            break;


        default:
            OutputToClient(pEcb, "\r\nUnexpected error: ");
            OutputToClient(pEcb, buffer);
            break;
        }
        return hr;
    }


    if (dwResult < 1) {
        OutputToClient(pEcb, "\r\n\r\nGot failure result from inside dotnet");
        return hr;
    }

    OutputToClient(pEcb, "\r\n Function table: SUCCESS!");

    ULONGLONG nSharedMemValue = 0;
    BOOL bGotValue = GetSharedMem(result);
    if (! bGotValue)
    {
        result = 0;
        return CO_E_NOT_SUPPORTED;
    }
    
    return S_OK;
}

// Call the .Net side to request function pointers for communication
// pszClassName:     Namespace and name of class containing the callback function
// pszFunctionName:  Function that provides callbacks. This *must* be of the form public static int MyFunc(string functionName)
// pEcb:             (Optional) http session to write logs to
HRESULT BuildFunctionTables(PCWSTR pszAssemblyPath, PCWSTR pszClassName, PCWSTR pszFunctionName, EXTENSION_CONTROL_BLOCK *pEcb)
{
    if (dnShutdown) return 0; // we've probably already built the tables

    ULONGLONG ptr = 0;
    HRESULT hr;

    // Make a call for each function pointer we need:

    hr = FindFunction(pszAssemblyPath, pszClassName, pszFunctionName, L"Shutdown", pEcb, &ptr);
    if (FAILED(hr)) return hr;
    dnShutdown = (VOID_FUNC_PTR)ptr;

    hr = FindFunction(pszAssemblyPath, pszClassName, pszFunctionName, L"Handle", pEcb, &ptr);
    if (FAILED(hr)) return hr;
    dnHandleRq = (ECB_FUNC_PTR)ptr;

    hr = FindFunction(pszAssemblyPath, pszClassName, pszFunctionName, L"Wakeup", pEcb, &ptr);
    if (FAILED(hr)) return hr;
    dnWakeup = (STR_STR_FUNC_PTR)ptr;

    return 0;
}

// Start .Net runtime for a specific assembly.
// pszVersion:       should be either "v2.0.50727" or "v4.0.30319"
// pszAssemblyPath:  full path to assembly
// pEcb:             (Optional) http session to write logs to
HRESULT StartDotnet(PCWSTR pszVersion, EXTENSION_CONTROL_BLOCK *pEcb)
{
    HRESULT hr;

    // 
    // Load and start the .NET runtime.
    // 

    OutputToClient(pEcb, "\r\nLoad and start the .NET runtime");
    OutputToClient(pEcb, "\r\n CLRCreateInstance...");

    if (pMetaHost == NULL) {
        hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_PPV_ARGS(&pMetaHost));
        if (FAILED(hr))
        {
            OutputToClient(pEcb, "\r\nCLRCreateInstance failed");
            ShutDownDotnet();
            return hr;
        }
    }
    else {
        OutputToClient(pEcb, "\r\npMetaHost already up");
    }

    OutputToClient(pEcb, "\r\n ICLRMetaHost::GetRuntime...");
    if (pRuntimeInfo == NULL) {
        // Get the ICLRRuntimeInfo corresponding to a particular CLR version. It 
        // supersedes CorBindToRuntimeEx with STARTUP_LOADER_SAFEMODE.
        hr = pMetaHost->GetRuntime(pszVersion, IID_PPV_ARGS(&pRuntimeInfo));
        if (FAILED(hr))
        {
            OutputToClient(pEcb, "\r\nICLRMetaHost::GetRuntime failed");
            ShutDownDotnet();
            return hr;
        }
        // Check if the specified runtime can be loaded into the process. This 
        // method will take into account other runtimes that may already be 
        // loaded into the process and set pbLoadable to TRUE if this runtime can 
        // be loaded in an in-process side-by-side fashion. 
        BOOL fLoadable;
        hr = pRuntimeInfo->IsLoadable(&fLoadable);
        if (FAILED(hr))
        {
            OutputToClient(pEcb, "\r\nICLRRuntimeInfo::IsLoadable failed");
            ShutDownDotnet();
            return hr;
        }

        if (!fLoadable)
        {
            OutputToClient(pEcb, "\r\n.NET runtime cannot be loaded");
            ShutDownDotnet();
            return hr;
        }
    }
    else {
        OutputToClient(pEcb, "\r\npRuntimeInfo already up");
    }

    OutputToClient(pEcb, "\r\n ICLRRuntimeInfo::GetInterface...");
    if (pClrRuntimeHost == NULL) {
        // Load the CLR into the current process and return a runtime interface 
        // pointer. ICorRuntimeHost and ICLRRuntimeHost are the two CLR hosting  
        // interfaces supported by CLR 4.0. Here we demo the ICLRRuntimeHost 
        // interface that was provided in .NET v2.0 to support CLR 2.0 new 
        // features. ICLRRuntimeHost does not support loading the .NET v1.x 
        // runtimes.
        hr = pRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_PPV_ARGS(&pClrRuntimeHost));
        if (FAILED(hr))
        {
            OutputToClient(pEcb, "\r\nICLRRuntimeInfo::GetInterface failed");
            ShutDownDotnet();
            return hr;
        }
    }
    else {
        OutputToClient(pEcb, "\r\npClrRuntimeHost already up");
    }

    // Start the CLR.
    OutputToClient(pEcb, "\r\n Host Start...");
    hr = pClrRuntimeHost->Start();
    if (FAILED(hr))
    {
        OutputToClient(pEcb, "\r\nCLR failed to start");
        ShutDownDotnet();
        return hr;
    }


    OutputToClient(pEcb, "\r\n .Net CLR is running");

    return hr;
}




const char* seDescription(DWORD code)
{
    switch (code) {
    case EXCEPTION_ACCESS_VIOLATION:         return "EXCEPTION_ACCESS_VIOLATION";
    case EXCEPTION_ARRAY_BOUNDS_EXCEEDED:    return "EXCEPTION_ARRAY_BOUNDS_EXCEEDED";
    case EXCEPTION_BREAKPOINT:               return "EXCEPTION_BREAKPOINT";
    case EXCEPTION_DATATYPE_MISALIGNMENT:    return "EXCEPTION_DATATYPE_MISALIGNMENT";
    case EXCEPTION_FLT_DENORMAL_OPERAND:     return "EXCEPTION_FLT_DENORMAL_OPERAND";
    case EXCEPTION_FLT_DIVIDE_BY_ZERO:       return "EXCEPTION_FLT_DIVIDE_BY_ZERO";
    case EXCEPTION_FLT_INEXACT_RESULT:       return "EXCEPTION_FLT_INEXACT_RESULT";
    case EXCEPTION_FLT_INVALID_OPERATION:    return "EXCEPTION_FLT_INVALID_OPERATION";
    case EXCEPTION_FLT_OVERFLOW:             return "EXCEPTION_FLT_OVERFLOW";
    case EXCEPTION_FLT_STACK_CHECK:          return "EXCEPTION_FLT_STACK_CHECK";
    case EXCEPTION_FLT_UNDERFLOW:            return "EXCEPTION_FLT_UNDERFLOW";
    case EXCEPTION_ILLEGAL_INSTRUCTION:      return "EXCEPTION_ILLEGAL_INSTRUCTION";
    case EXCEPTION_IN_PAGE_ERROR:            return "EXCEPTION_IN_PAGE_ERROR";
    case EXCEPTION_INT_DIVIDE_BY_ZERO:       return "EXCEPTION_INT_DIVIDE_BY_ZERO";
    case EXCEPTION_INT_OVERFLOW:             return "EXCEPTION_INT_OVERFLOW";
    case EXCEPTION_INVALID_DISPOSITION:      return "EXCEPTION_INVALID_DISPOSITION";
    case EXCEPTION_NONCONTINUABLE_EXCEPTION: return "EXCEPTION_NONCONTINUABLE_EXCEPTION";
    case EXCEPTION_PRIV_INSTRUCTION:         return "EXCEPTION_PRIV_INSTRUCTION";
    case EXCEPTION_SINGLE_STEP:              return "EXCEPTION_SINGLE_STEP";
    case EXCEPTION_STACK_OVERFLOW:           return "EXCEPTION_STACK_OVERFLOW";
    case 0xE0434352:                         return "CLR Error";
    default: return "UNKNOWN EXCEPTION";
    }
}