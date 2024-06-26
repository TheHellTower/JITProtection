#include "stdafx.h"
#include <windows.h>
#include <stdlib.h>
#include <stdio.h>
#include <tchar.h>
#include <CorHdr.h>
#include "corinfo.h"
#include "corjit.h"
#include <fstream>
#include <iostream>
#include "Base64.h"
#pragma warning(disable:4996)

using namespace std;

HINSTANCE hInstance;

void LogInfo(const char *className, const char *methodName);
int __stdcall my_compileMethod(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode);
ULONG_PTR *(__stdcall *p_getJit)();
typedef int(__stdcall *compileMethod_def)(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode);
typedef ICorJitCompiler* (__stdcall* pGetJitFn)();
compileMethod_def compileMethod;
BOOL bHooked = FALSE;
BYTE *CODE;
std::vector<BYTE> key = base64_decode("W2h0dHBzOi8vZ2l0aHViLmNvbS9UaGVIZWxsVG93ZXIgfCBodHRwczovL2NyYWNrZWQuaW8vVGhlSGVsbFRvd2VyXSB4NjQgc3VwcG9ydCAmIGEgZmV3IG90aGVyIHRoaW5ncyB8IDA2LzI2LzIwMjQ=");

struct JIT
{
	compileMethod_def compileMethod;
};

#include <windows.h>
#include <vector>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  dwReason, LPVOID lpReserved)
{
	//hInstance = (HINSTANCE)hModule;
	hInstance = GetModuleHandleA(nullptr);
	return TRUE;
}


extern "C" __declspec(dllexport) void Invoke()
{
	if (bHooked) return;

	LoadLibrary(_T("clr.dll"));

	HMODULE hJitMod = LoadLibrary(_T("clrjit.dll"));

	if (!hJitMod)
		return;

	p_getJit = (ULONG_PTR *(__stdcall *)()) GetProcAddress(hJitMod, "getJit");

	//pGetJitFn getJitFn = (pGetJitFn)GetProcAddress(hJitMod, "getJit");
	//ICorJitCompiler* pICorJitCompiler = (*getJitFn)();

	if (p_getJit)
	{
		JIT *pJit = (JIT *) *((ULONG_PTR *)p_getJit());

		if (pJit)
		{
			DWORD OldProtect;
			VirtualProtect(pJit, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);
			compileMethod = pJit->compileMethod;
			pJit->compileMethod = &my_compileMethod;
			VirtualProtect(pJit, sizeof(ULONG_PTR), OldProtect, &OldProtect);
			bHooked = TRUE;
		}
	}
}

int __stdcall my_compileMethod(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode)
{
	const char *szMethodName = NULL;
	const char *szClassName = NULL;
	szMethodName = comp->getMethodName(info->ftn, &szClassName);

	bool ok = true;
	BYTE first = (BYTE)(info->ILCode[0] ^ key[0]);
	for (int i = 1; i < 5; i++)
		if ((BYTE)(info->ILCode[i] ^ key[i % key.size()]) != first)
			ok = false;
	
	if(ok)
	{
		DWORD OldProtect;
		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);
		
		for (int i = 0; i < info->ILCodeSize; i++)
			info->ILCode[i] = (BYTE)(info->ILCode[i] ^ key[i % key.size()]);

		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), OldProtect, &OldProtect);
	}

	int nRet = compileMethod(classthis, comp, info, flags, nativeEntry, nativeSizeOfCode);

	if (ok)
	{
		DWORD OldProtect;
		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);

		for (int i = 0; i < info->ILCodeSize; i++)
			info->ILCode[i] = (BYTE)(info->ILCode[i] ^ key[i % key.size()]);

		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), OldProtect, &OldProtect);
	}

	return nRet;
}