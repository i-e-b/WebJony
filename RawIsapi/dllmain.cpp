// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"

wchar_t Global_DllFilePath[513] = { 0 };

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    {
        GetModuleFileNameW(hModule, (LPWSTR)Global_DllFilePath, 512); // comes out like `\\?\C:\Gits\RawIsapi\x64\Debug\RawIsapi.dll`

        // Find last backslash
        size_t pos = wcslen(Global_DllFilePath);
        for (; pos > 0; pos--)
        {
            if (Global_DllFilePath[pos] == L'\\') {
                pos++;
                break;
            }
        }

        // Replace with expected .Net dll name
        const wchar_t *expectedName = DOTNET_HOST_FILE_NAME;
        int i = 0;
        do { Global_DllFilePath[pos] = expectedName[i]; pos++; i++; } while (expectedName[i] != 0);

        break;
    }

    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

