// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "PipeMessage.h"
#include <Psapi.h>
#include <vector>
#include <iostream>
//#include <conio.h>
//#include <chrono>

using namespace std;

DWORD WINAPI MyThread(LPVOID);
DWORD g_threadID;
HMODULE g_hModule;
HANDLE pipe;
const char* PerformanceNoteFuncSignature = "\x55\x8b\xec\x56\x6a\x00\x8b\xf1\xe8\x00\x00\x00\x00\x83\xf8";
const char* PerformanceNoteFuncSigMask = "xxxxx?xxx????xx";
const char* PerformanceNoteP1Signature = "\x85\xf6\x74\x00\xc7\x06\x00\x00\x00\x00\xa0";
const char* PerformanceNoteP1SigMask = "xxx?xx????x";
const char* SlashInstanceFuncSignature = "\x55\x8b\xec\x56\x8b\x75\x00\x85\xf6\x75\x00\x8d\x46\x00\x5e\x5d\xc2\x00\x00\xb9";
const char* SlashInstanceFuncSigMask = "xxxxxx?xxx?xx?xxx??x";
const char* SlashInstanceP1Signature = "\x33\xc0\xf0\x0f\xc1\x03\x85\xc0\x00\x00\xff\x76\x10\x8b\x0d";
const char* SlashInstanceP1SigMask = "xxxxxxxx??xxxxx";
const char* SlashInstanceP2Signature = "\xe8\x00\x00\x00\x00\xe9\x00\x00\x00\x00\x8d\x8e\x00\x00\x00\x00\xe8\x00\x00\x00\x00\x8b\x0d";
const char* SlashInstanceP2SigMask = "x????x????xx????x????xx";
uintptr_t SlashInstanceFuncAddress;
uintptr_t* SlashInstancePAddress = new uintptr_t[2];
uintptr_t PerformanceNoteP1Address;
uintptr_t PerformanceNoteFuncAddress;
const unsigned char PerformanceNoteP1Offset = 0x9F;
void CallPerformanceNote(char);
void CallSlashInstance();
DWORD_PTR GetProcessBaseAddress(DWORD);
uintptr_t ResolvePointerPath(uintptr_t, vector<unsigned int>);
int ConnectToPipe();
int ReadPipe(PipeMessage& lastmsg);
DWORD GetModuleSize(DWORD);
uintptr_t FindPattern(uintptr_t, DWORD, const char[], const char[], bool = false);

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		g_hModule = hModule;
		DisableThreadLibraryCalls(hModule);
		CreateThread(NULL, NULL, &MyThread, NULL, NULL, &g_threadID);
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

DWORD WINAPI MyThread(LPVOID)
{
	//AllocConsole();
	//FILE* fp;
	int PID = GetCurrentProcessId();
	DWORD procAddr = GetProcessBaseAddress(PID);
	DWORD moduleSize = GetModuleSize(PID);
	DWORD mode = PIPE_READMODE_MESSAGE;
	/*freopen_s(&fp, "CONOUT$", "w", stdout);
	cout << "CONSOLE OUTPUT" << endl;
	typedef chrono::high_resolution_clock clock;
	auto start_time = clock::now();*/
	PerformanceNoteFuncAddress = FindPattern(procAddr, moduleSize, PerformanceNoteFuncSignature, PerformanceNoteFuncSigMask);
	PerformanceNoteP1Address = FindPattern(procAddr, moduleSize, PerformanceNoteP1Signature, PerformanceNoteP1SigMask, true);
	SlashInstanceFuncAddress = FindPattern(procAddr, moduleSize, SlashInstanceFuncSignature, SlashInstanceFuncSigMask);
	SlashInstancePAddress[0] = FindPattern(procAddr, moduleSize, SlashInstanceP1Signature, SlashInstanceP1SigMask, true);
	SlashInstancePAddress[1] = FindPattern(procAddr, moduleSize, SlashInstanceP2Signature, SlashInstanceP2SigMask, true);
	/*auto end_time = clock::now();
	auto milliseconds = chrono::duration_cast<chrono::milliseconds>(end_time - start_time).count();
	cout << dec << milliseconds << "ms" << endl;
	cout << "PerformanceNoteFuncAddress = " << hex << PerformanceNoteFuncAddress << endl;
	cout << "PerformanceNoteP1Address = " << PerformanceNoteP1Address + PerformanceNoteP1Offset << endl;
	cout << "SlashInstanceFuncAddress = " << SlashInstanceFuncAddress << endl;
	cout << "SlashInstancePAddress[0] = " << SlashInstancePAddress[0] << endl;
	cout << "SlashInstancePAddress[1] = " << SlashInstancePAddress[1] << endl;
	cout << "Connecting to pipe..." << endl;*/
	if (ConnectToPipe() == 0)
	{
		cout << pipe << endl;
		SetNamedPipeHandleState(pipe, &mode, nullptr, nullptr);
		cout << "Reading data from pipe..." << endl;
		PipeMessage lastRecMsg;
		while (ReadPipe(lastRecMsg) != 0)
		{
			if (lastRecMsg.Cmd == Exit)
				break;
			else if (lastRecMsg.PID == PID)
			{
				if (lastRecMsg.Cmd == SlashInstance) {
					CallSlashInstance();
				}
				else if (lastRecMsg.Cmd == PlayNote) {
					CallPerformanceNote(lastRecMsg.Parameter);
				}
			}
		}
		CloseHandle(pipe);
	}
	//system("pause");
	//fclose(fp);
	//FreeConsole();
	FreeLibraryAndExitThread(g_hModule, 0);
	//return 0;
}

int ReadPipe(PipeMessage& lastmsg)
{
	char buffer[128];
	DWORD numBytesRead;
	int result = ReadFile(
		pipe,
		buffer, //the data from the pipe will be put here
		127 * sizeof(char), //number of bytes allocated
		&numBytesRead, //this will store number of bytes actually read
		NULL //not using overlapped IO
	);
	if (result) {
		buffer[numBytesRead / sizeof(char)] = '\0'; // null terminate the string
		PipeMessage* msgp = (PipeMessage*)buffer;
		//cout << "Number of bytes read: " << numBytesRead << endl;
		cout << "PipeMessage.PID: " << msgp->PID << endl;
		cout << "PipeMessage.Cmd: " << msgp->Cmd << endl;
		if (msgp->Cmd == PlayNote)
			cout << "PipeMessage.Parameter: " << msgp->Parameter << endl;
		lastmsg = *msgp;
	}
	else {
		//cout << "Failed to read data from the pipe." << endl;
	}
	return result;
}

int ConnectToPipe()
{
	// Try to open a named pipe; wait for it, if necessary. 
	LPTSTR lpszPipename = TEXT("\\\\.\\pipe\\XIV-Hunt");
	while (1)
	{
		pipe = CreateFile(
			lpszPipename,   //pipe name 
			GENERIC_READ,	//read access 
			0,              //no sharing 
			NULL,           //default security attributes
			OPEN_EXISTING,  //opens existing pipe 
			0,              //default attributes 
			NULL);          //no template file 

		//Break if the pipe handle is valid. 
		if (pipe != INVALID_HANDLE_VALUE)
			break;

		// Exit if an error other than ERROR_PIPE_BUSY occurs. 
		if (pipe == INVALID_HANDLE_VALUE)
		{
			cout << "INVALID_HANDLE_VALUE: " << GetLastError() << endl;
			return -1;
		}

		// All pipe instances are busy, so wait for 20 seconds. 
		if (!WaitNamedPipe(lpszPipename, 20000))
		{
			cout << "Could not open pipe: 20 second wait timed out." << endl;
			return -1;
		}
	}
	return 0;
}

uintptr_t ResolvePointerPath(uintptr_t b, vector<unsigned int> offsets)
{
	for (auto &i : offsets)
		b = *reinterpret_cast<uintptr_t *>(b) + i;
	return b;
}

void CallPerformanceNote(char noteID)
{
	if (PerformanceNoteFuncAddress == NULL || PerformanceNoteP1Address == NULL /*|| noteID < 0x18 || noteID > 0x3C*/) //game only has these notes
		return;
	typedef void(__thiscall *pFunctionAddress)(uintptr_t, char);
	pFunctionAddress PerformanceNoteFunctionPointer = (pFunctionAddress)(PerformanceNoteFuncAddress);
	PerformanceNoteFunctionPointer(PerformanceNoteP1Address + PerformanceNoteP1Offset, noteID);
}

void CallSlashInstance()
{
	if (SlashInstanceFuncAddress == NULL || SlashInstancePAddress[0] == NULL || SlashInstancePAddress[1] == NULL || SlashInstancePAddress[2] == NULL)
		return;
	typedef int(__stdcall *pFunctionAddress)(int, int);//EBX, ESI
	int EBX, ESI;
	EBX = ResolvePointerPath(SlashInstancePAddress[0], { 0x1468, 0x2DB4, 0x854 });
	ESI = ResolvePointerPath(SlashInstancePAddress[1], { 0x0, 0x0 });
	pFunctionAddress SlashInstanceFunctionPointer = (pFunctionAddress)(SlashInstanceFuncAddress);
	SlashInstanceFunctionPointer(EBX, ESI);
}

DWORD_PTR GetProcessBaseAddress(DWORD processID)
{
	DWORD_PTR   baseAddress = 0;
	HANDLE      processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
	HMODULE     *moduleArray;
	LPBYTE      moduleArrayBytes;
	DWORD       bytesRequired;

	if (processHandle)
	{
		if (EnumProcessModules(processHandle, NULL, 0, &bytesRequired))
		{
			if (bytesRequired)
			{
				moduleArrayBytes = (LPBYTE)LocalAlloc(LPTR, bytesRequired);
				if (moduleArrayBytes)
				{
					unsigned int moduleCount;
					moduleCount = bytesRequired / sizeof(HMODULE);
					moduleArray = (HMODULE *)moduleArrayBytes;
					if (EnumProcessModules(processHandle, moduleArray, bytesRequired, &bytesRequired))
					{
						baseAddress = (DWORD_PTR)moduleArray[0];
					}
					LocalFree(moduleArrayBytes);
				}
			}
		}
		CloseHandle(processHandle);
	}
	return baseAddress;
}

DWORD GetModuleSize(DWORD processID)
{
	HANDLE      processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
	MODULEINFO mi;
	GetModuleInformation(processHandle, GetModuleHandle(L"ffxiv.exe"), &mi, sizeof(mi)/* + 4*/);//+4 to prevent ERROR_INSUFFICIENT_BUFFER
	return mi.SizeOfImage;
}

uintptr_t FindPattern(uintptr_t base, DWORD size, const char pattern[], const char mask[], bool follow1st)
{
	for (uintptr_t retAddress = base; retAddress < (base + size); retAddress++)
	{
		if (*(BYTE*)retAddress == (pattern[0] & 0xff) || mask[0] == '?')
		{
			uintptr_t startSearch = retAddress;
			for (int i = 0; mask[i] != '\0'; i++, startSearch++)
			{
				if (mask[i] == '?')
					continue;
				if ((pattern[i] & 0xff) != *(BYTE*)startSearch)
					break;
				if ((pattern[i] & 0xff) == *(BYTE*)startSearch && mask[i + 1] == '\0')
				{
					if (follow1st)
					{
						return *reinterpret_cast<unsigned int*>(retAddress + i + 1);
					}
					return retAddress;
				}
			}
		}
	}
	return NULL;
}