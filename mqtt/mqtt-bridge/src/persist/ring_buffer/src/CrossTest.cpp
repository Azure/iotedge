// CrossTest.cpp : Defines the entry point for the application.

//

 

#define _CRT_SECURE_NO_WARNINGS 1

 

#include <thread>

#include <iostream>

//#include <fileapi.h>

//#include <unistd.h>

//#include "CrossTest.h"

 

using namespace std;

 

 

 

 

 

 

static int rnd_seed = 100 % 2147483647;

 

static int GetNextRand()

{

    return abs(rnd_seed = rnd_seed * 16807 % 2147483647);

}

 

static int GetNextRand(int min, int max)

{

    int result = GetNextRand();

 

    result = result % (max - min);

    result += min;

 

    return result;

}

 

static uint8_t GetNextByte()

{

    return (uint8_t)(GetNextRand() % 256);

}

 

static void FillBuffer(uint8_t buffer[], int iSize)

{

    for (int i = 0; i < iSize; i++)

    {

        buffer[i] = GetNextByte();

    }

}

 

 

 

 

typedef struct

{

    uint32_t state[5];

    uint32_t count[2];

    unsigned char buffer[64];

} SHA1_CTX;

 

void SHA1Transform(

    uint32_t state[5],

    const unsigned char buffer[64]

);

 

void SHA1Init(

    SHA1_CTX* context

);

 

void SHA1Update(

    SHA1_CTX* context,

    const unsigned char* data,

    uint32_t len

);

 

void SHA1Final(

    unsigned char digest[20],

    SHA1_CTX* context

);

 

void SHA1(

    char* hash_out,

    const char* str,

    int len);

 

 

 

#define NO_FREE_INDEX -1

#define NO_USED_INDEX -1

 

struct FileQueue {

    int iFirstUsedByteIndex;

    int iFirstFreeByteIndex;

 

    int iSize;

 

    FILE* pFile;

 

    int GetMaxReadableSegment() {

        // figure out the different cases:

 

        if (this->iFirstFreeByteIndex != NO_FREE_INDEX && this->iFirstUsedByteIndex != NO_USED_INDEX) {

            // 1) [...free...| ...data...]

            // 2) [...data...|...free...|...data...]

            if (this->iFirstFreeByteIndex < this->iFirstUsedByteIndex) {

                return this->iSize - this->iFirstUsedByteIndex;

            }

 

            // FirstFree and First used cannot be equal because we set NO_XXXX_INDEX for that case

 

            // 3) [...data...|...free...]

            // 4) [...free...|...data...|...free...]

            if (this->iFirstFreeByteIndex > this->iFirstUsedByteIndex) {

                return this->iFirstFreeByteIndex - this->iFirstUsedByteIndex;

            }

        }

 

        // 5) no data at all

        if (this->iFirstUsedByteIndex == NO_USED_INDEX) {

            return 0;

        }

 

        // 6) no free place at all

        if (this->iFirstFreeByteIndex == NO_FREE_INDEX) {

            return this->iSize - this->iFirstUsedByteIndex;

        }

 

        throw "this should not happen";

    }

 

    int GetMaxSpace() {

        // figure out the different cases:

 

        if (this->iFirstFreeByteIndex != NO_FREE_INDEX && this->iFirstUsedByteIndex != NO_USED_INDEX) {

            // 1) [...free...| ...data...]

            // 2) [...data...|...free...|...data...]

            if (this->iFirstFreeByteIndex < this->iFirstUsedByteIndex) {

                return this->iFirstUsedByteIndex - this->iFirstFreeByteIndex;

            }

 

            // FirstFree and First used cannot be equal because we set NO_XXXX_INDEX for that case

 

            // 3) [...data...|...free...]

            // 4) [...free...|...data...|...free...]

            if (this->iFirstFreeByteIndex > this->iFirstUsedByteIndex) {

                return   (this->iSize - this->iFirstFreeByteIndex)       // top half

                    + this->iFirstUsedByteIndex;                       // bottom half

            }

        }

 

        // 5) no data at all

        if (this->iFirstUsedByteIndex == NO_USED_INDEX) {

            return this->iSize;

        }

 

        // 6) no free place at all

        if (this->iFirstFreeByteIndex == NO_FREE_INDEX) {

            return 0;

        }

 

        throw "this should not happen";

    }

 

    // differs from GetMaxSpace() that it gives the size of the first write

    // in case 4) when the free space is split

    int GetMaxSegment() {

 

        // this is the spilt case scenario

        if (this->iFirstFreeByteIndex != NO_FREE_INDEX &&

            this->iFirstUsedByteIndex != NO_USED_INDEX &&

            this->iFirstFreeByteIndex > this->iFirstUsedByteIndex&&

            this->iFirstUsedByteIndex > 0) {

 

            // count only the top half

            return this->iSize - this->iFirstFreeByteIndex;

        }

        // if no data, but for some reason the first free index is not 0,

        // then even if the full buffer is free, we can write only up the the last byte (without overflow)

        else if (this->iFirstFreeByteIndex != NO_FREE_INDEX &&

            this->iFirstUsedByteIndex == NO_USED_INDEX) {

            return this->iSize - this->iFirstFreeByteIndex;

        }

        else {

            return GetMaxSpace();

        }

    }

 

    void Dump() {

        cout << "Ring state:" << "\n";

        cout << "\tFirst Free: " << this->iFirstFreeByteIndex << "\n";

        cout << "\tFirst Used: " << this->iFirstUsedByteIndex << "\n";

        cout << "\tSize: " << this->iSize << "\n";

    }

};

 

uint8_t* CreateBoxForData(uint8_t* pData, int iSize);

 

FileQueue* CreateQueue(int iSize, const char* pName);

void CloseQueue(FileQueue* pQueue);

void Enqueue(uint8_t* pData, int iSize, FileQueue* pQueue);

uint8_t* Dequeue(FileQueue* pQueue);

 

 

void SecondLoop(const char* pFilename) {

    cout << "\n\nfrom second loop\n\n";

    FileQueue* pQueue = CreateQueue(90960, pFilename);

 

    int minPacketSize = 100;

    int maxPacketSize = 200;

 

    for (int i = 0; i < 10000; i++)

    {

        int nextPacketSize = GetNextRand(minPacketSize, maxPacketSize);

        uint8_t* nextPacket = new uint8_t[nextPacketSize];

 

        FillBuffer(nextPacket, nextPacketSize);

 

        Enqueue(nextPacket, nextPacketSize, pQueue);

 

        //cout << "\n\nadded: " << nextPacketSize << " bytes\n\n";

        //pQueue->Dump();

 

        delete[] nextPacket;

 

        if (i > 10)

        {

            //cout << "\n\nremoving\n\n";

            Dequeue(pQueue);

            //  pQueue->Dump();

        }

    }

 

    cout << "\n\nfinished 2\n\n";

}

 

void GeneratePackets(FileQueue* pQueue) {

 

    /*  std::thread first(SecondLoop, "testfile2.bin");

      std::thread second(SecondLoop, "testfile3.bin");

      std::thread third(SecondLoop, "testfile4.bin");

      std::thread fourth(SecondLoop, "testfile5.bin");

      std::thread fifth(SecondLoop, "testfile6.bin");

      std::thread sixth(SecondLoop, "testfile7.bin");

      std::thread seventh(SecondLoop, "testfile8.bin");*/

 

    int minPacketSize = 100;

    int maxPacketSize = 200;

 

    for (int i = 0; i < 100000; i++)

    {

        int nextPacketSize = GetNextRand(minPacketSize, maxPacketSize);

        uint8_t* nextPacket = new uint8_t[nextPacketSize];

 

        FillBuffer(nextPacket, nextPacketSize);

 

        Enqueue(nextPacket, nextPacketSize, pQueue);

 

        //cout << "\n\nadded: " << nextPacketSize << " bytes\n\n";

        //pQueue->Dump();

 

        delete[] nextPacket;

 

        if (i > 10)

        {

            //cout << "\n\nremoving\n\n";

            Dequeue(pQueue);

            //  pQueue->Dump();

        }

    }

 

    cout << "\n\nfinished\n\n";

    /*   first.join();

       second.join();

       third.join();

       fourth.join();

       fifth.join();

       sixth.join();

       seventh.join();*/

}

 

 

int main() {

 

    try {

 

        cout << "doing something...." << endl;

 

 

        FileQueue* pQueue = CreateQueue(90960, "testfile1.bin");

 

        time_t start, finish;

        time(&start);

 

 

        GeneratePackets(pQueue);

 

        time(&finish);

        cout << "Time required = " << difftime(finish, start) << " seconds";

 

        /*

        uint8_t* pBuffer = new uint8_t[1024];

 

        int i;

        for (i = 0; i < 1024; i++)

            pBuffer[i] = i % 256;

 

 

        uint8_t* pRead;

 

        for (i = 0; i < 10; i++) {

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            Enqueue(pBuffer, 1024 - 73, pQueue);

            pQueue->Dump();

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            Enqueue(pBuffer, 1024, pQueue);

            pQueue->Dump();

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

            pRead = Dequeue(pQueue);

            pQueue->Dump();

            delete[] pRead;

 

        }

 

        for (i = 0; i < 10; i++) {

           pRead = Dequeue(pQueue);

           pQueue->Dump();

           delete[] pRead;

        }

        */

        CloseQueue(pQueue);

 

        //  delete[] pBuffer;

 

        return 0;

    }

    catch (const char* pMsg) {

        cout << "dieing " << pMsg << "\n";

    }

}

 

 

FileQueue* CreateQueue(int iSize, const char* pName) {

    FILE* pFile;

    pFile = fopen(pName, "w+");

 

    FileQueue* pQueue = new FileQueue();

    fseek(pFile, iSize - 1, SEEK_SET);

 

    pQueue->pFile = pFile;

    pQueue->iSize = iSize;

 

    pQueue->iFirstFreeByteIndex = 0;

    pQueue->iFirstUsedByteIndex = NO_USED_INDEX;

 

    return pQueue;

}

 

void CloseQueue(FileQueue* pQueue) {

    fclose(pQueue->pFile);

}

 

void Enqueue(uint8_t* pData, int iSize, FileQueue* pQueue) {

    int maxSpace = pQueue->GetMaxSpace();

    int maxSegment = pQueue->GetMaxSegment();

 

    if (maxSpace < iSize + 18) {

        throw "out of space";

    }

 

    // add the frame to the data

    uint8_t* pBoxed = CreateBoxForData(pData, iSize);

    iSize += 18; // add the frame size

 

    // save the fist start index, because maybe this will be the first

    // data, so FirstUsed will be pointing there

    int originalFirstFree = pQueue->iFirstFreeByteIndex;

 

    //cout << "max segment: " << maxSegment << "\n";

 

    int firstSegmentLen = 0;

 

    // now we have enough space, but do we need to split the write?

    if (maxSegment < iSize) {

        // this will be the split write case

        fseek(pQueue->pFile, pQueue->iFirstFreeByteIndex, SEEK_SET);

        fwrite(pBoxed, 1, maxSegment, pQueue->pFile);

 

        // now we know that we filled up the upper segment, so FirstFree should be reset to zero

        pQueue->iFirstFreeByteIndex = 0;

 

        // also adjust the incoming parameters:

        firstSegmentLen = maxSegment;

        iSize = iSize - maxSegment;

 

        // and now lets flow to the "normal" case:

    }

 

    // this is the "normal" scenario when the data would fit

    fseek(pQueue->pFile, pQueue->iFirstFreeByteIndex, SEEK_SET);

    fwrite(pBoxed + firstSegmentLen, 1, iSize, pQueue->pFile);

   // fdatasync(fileno(pQueue->pFile));

 

    pQueue->iFirstFreeByteIndex += iSize;

 

    // it is possible that we ran out of space:

    if (pQueue->iFirstFreeByteIndex == pQueue->iFirstUsedByteIndex) {

        pQueue->iFirstFreeByteIndex = NO_FREE_INDEX;

    }

 

    // it is possible that stopped right at the end:

    if (pQueue->iFirstFreeByteIndex == pQueue->iSize) {

        pQueue->iFirstFreeByteIndex = 0;

    }

 

    // it is possible that this is the first datablock

    if (pQueue->iFirstUsedByteIndex == NO_USED_INDEX) {

        pQueue->iFirstUsedByteIndex = originalFirstFree;

    }

 

    delete[] pBoxed;

}

 

uint8_t* Dequeue(FileQueue* pQueue) {

    if (pQueue->iFirstUsedByteIndex == NO_USED_INDEX) {

        throw "no data to read";

    }

 

    int iOriginalFirstUsedIndex = pQueue->iFirstUsedByteIndex;

 

    fseek(pQueue->pFile, pQueue->iFirstUsedByteIndex, SEEK_SET);

 

    uint8_t size[4];

 

    // can we load the size field with one read?

    if (pQueue->iSize - pQueue->iFirstUsedByteIndex >= 4) {

        fread(size, 4, 1, pQueue->pFile);

 

        pQueue->iFirstUsedByteIndex += 4;

    }

    else {

        fread(size, pQueue->iSize - pQueue->iFirstUsedByteIndex, 1, pQueue->pFile);

        fseek(pQueue->pFile, 0, SEEK_SET);

        fread(size + pQueue->iSize - pQueue->iFirstUsedByteIndex, 4 - (pQueue->iSize - pQueue->iFirstUsedByteIndex), 1, pQueue->pFile);

 

        pQueue->iFirstUsedByteIndex = 4 - (pQueue->iSize - pQueue->iFirstUsedByteIndex);

    }

 

    int iSize = ((int)size[0] << 24) | ((int)size[1] << 16) | ((int)size[2] << 8) | ((int)size[3]);

 

    iSize += 4 + 10;

 

    int iMaxDataSize = pQueue->iSize - pQueue->GetMaxSpace();

 

    if (iSize > iMaxDataSize) {

        throw "block size doesn't fit to ring state";

    }

 

    int iMaxReadableSegment = pQueue->GetMaxReadableSegment();

 

    //cout << "Reading size: " << iSize << "\n";

    //cout << "max segment: " << iMaxReadableSegment << "\n";

 

    uint8_t* pResultBox = new uint8_t[iSize];

 

    int iFirstBlockLen = iSize > iMaxReadableSegment ? iMaxReadableSegment : iSize;

 

    fread(pResultBox, iFirstBlockLen, 1, pQueue->pFile);

    pQueue->iFirstUsedByteIndex += iFirstBlockLen;

 

    // is it possible, that the block reached exaclty till the end of the ring

    if (pQueue->iFirstUsedByteIndex == pQueue->iSize) {

        pQueue->iFirstUsedByteIndex = 0;

    }

 

    // if it was a partial read, then First Used overflows

    if (iFirstBlockLen < iSize) {

        fseek(pQueue->pFile, 0, SEEK_SET);

        fread(pResultBox, iSize - iFirstBlockLen, 1, pQueue->pFile);

 

        pQueue->iFirstUsedByteIndex = iSize - iFirstBlockLen;

    }

 

    // this is a super special case, when a single record consumed the whole

    // ring - in this case we get back to the original FirstUsedIndex

    if (iOriginalFirstUsedIndex == pQueue->iFirstUsedByteIndex) {

        pQueue->iFirstUsedByteIndex = NO_USED_INDEX;

        pQueue->iFirstFreeByteIndex = 0; // let's roll back to the start of the ring

    }

    // otherwise we might have read the last segment, reaching the first free byte

    else if (pQueue->iFirstUsedByteIndex == pQueue->iFirstFreeByteIndex) {

        pQueue->iFirstUsedByteIndex = NO_USED_INDEX;

        pQueue->iFirstFreeByteIndex = 0; // let's roll back to the start of the ring

    }

    // if there was no free segment, now we have

    else if (pQueue->iFirstFreeByteIndex == NO_FREE_INDEX) {

        pQueue->iFirstFreeByteIndex = iOriginalFirstUsedIndex;

    }

 

    uint8_t zero[1];

    zero[0] = 0;

    fseek(pQueue->pFile, iOriginalFirstUsedIndex, SEEK_SET);

    fwrite(zero, 1, 1, pQueue->pFile);

//fdatasync(fileno(pQueue->pFile));

 

    // we should remove the decorator and hash, but for noe just return the read record as is

    return pResultBox;

}

 

uint8_t* CreateBoxForData(uint8_t* pData, int iSize) {

    // we add 32 bit size, 32 bit decorator, the data, and 10 bytes hash

    // that is 18 bytes overhead:

    uint8_t* pResult = new uint8_t[iSize + 18];

 

    // encode the size:

    pResult[0] = (uint8_t)((iSize >> 24) & 0xff);

    pResult[1] = (uint8_t)((iSize >> 16) & 0xff);

    pResult[2] = (uint8_t)((iSize >> 8) & 0xff);

    pResult[3] = (uint8_t)((iSize) & 0xff);

 

    // the decor is just a pattern

    pResult[4] = 0xcd;

    pResult[5] = 0xcd;

    pResult[6] = 0xcd;

    pResult[7] = 0xcd;

 

    // now the real data

    memcpy(pResult + 8, pData, iSize);

 

    // calculate the hash of all the previous

    SHA1_CTX hashctx;

 

    SHA1Init(&hashctx);

    SHA1Update(&hashctx, pResult, 8 + iSize);

 

    uint8_t hash[20];

 

    SHA1Final(hash, &hashctx);

 

    // use the first 10 bytes of the hash only

    memcpy(pResult + 8 + iSize, hash, 10);

 

    return pResult;

}

 

 

 

 

 

 

 

 

 

 

 

 

 

 

 

#define rol(value, bits) (((value) << (bits)) | ((value) >> (32 - (bits))))

 

/* blk0() and blk() perform the initial expand. */

/* I got the idea of expanding during the round function from SSLeay */

#if BYTE_ORDER == LITTLE_ENDIAN

#define blk0(i) (block->l[i] = (rol(block->l[i],24)&0xFF00FF00) \
    |(rol(block->l[i],8)&0x00FF00FF))

#elif BYTE_ORDER == BIG_ENDIAN

#define blk0(i) block->l[i]

#else

#error "Endianness not defined!"

#endif

#define blk(i) (block->l[i&15] = rol(block->l[(i+13)&15]^block->l[(i+8)&15] \
    ^block->l[(i+2)&15]^block->l[i&15],1))

 

/* (R0+R1), R2, R3, R4 are the different operations used in SHA1 */

#define R0(v,w,x,y,z,i) z+=((w&(x^y))^y)+blk0(i)+0x5A827999+rol(v,5);w=rol(w,30);

#define R1(v,w,x,y,z,i) z+=((w&(x^y))^y)+blk(i)+0x5A827999+rol(v,5);w=rol(w,30);

#define R2(v,w,x,y,z,i) z+=(w^x^y)+blk(i)+0x6ED9EBA1+rol(v,5);w=rol(w,30);

#define R3(v,w,x,y,z,i) z+=(((w|x)&y)|(w&x))+blk(i)+0x8F1BBCDC+rol(v,5);w=rol(w,30);

#define R4(v,w,x,y,z,i) z+=(w^x^y)+blk(i)+0xCA62C1D6+rol(v,5);w=rol(w,30);

 

 

/* Hash a single 512-bit block. This is the core of the algorithm. */

 

void SHA1Transform(

    uint32_t state[5],

    const unsigned char buffer[64]

)

{

    uint32_t a, b, c, d, e;

 

    typedef union

    {

        unsigned char c[64];

        uint32_t l[16];

    } CHAR64LONG16;

 

 

    CHAR64LONG16 block[1];      /* use array to appear as a pointer */

 

    memcpy(block, buffer, 64);

 

    /* Copy context->state[] to working vars */

    a = state[0];

    b = state[1];

    c = state[2];

    d = state[3];

    e = state[4];

    /* 4 rounds of 20 operations each. Loop unrolled. */

    R0(a, b, c, d, e, 0);

    R0(e, a, b, c, d, 1);

    R0(d, e, a, b, c, 2);

    R0(c, d, e, a, b, 3);

    R0(b, c, d, e, a, 4);

    R0(a, b, c, d, e, 5);

    R0(e, a, b, c, d, 6);

    R0(d, e, a, b, c, 7);

    R0(c, d, e, a, b, 8);

    R0(b, c, d, e, a, 9);

    R0(a, b, c, d, e, 10);

    R0(e, a, b, c, d, 11);

    R0(d, e, a, b, c, 12);

    R0(c, d, e, a, b, 13);

    R0(b, c, d, e, a, 14);

    R0(a, b, c, d, e, 15);

    R1(e, a, b, c, d, 16);

    R1(d, e, a, b, c, 17);

    R1(c, d, e, a, b, 18);

    R1(b, c, d, e, a, 19);

    R2(a, b, c, d, e, 20);

    R2(e, a, b, c, d, 21);

    R2(d, e, a, b, c, 22);

    R2(c, d, e, a, b, 23);

    R2(b, c, d, e, a, 24);

    R2(a, b, c, d, e, 25);

    R2(e, a, b, c, d, 26);

    R2(d, e, a, b, c, 27);

    R2(c, d, e, a, b, 28);

    R2(b, c, d, e, a, 29);

    R2(a, b, c, d, e, 30);

    R2(e, a, b, c, d, 31);

    R2(d, e, a, b, c, 32);

    R2(c, d, e, a, b, 33);

    R2(b, c, d, e, a, 34);

    R2(a, b, c, d, e, 35);

    R2(e, a, b, c, d, 36);

    R2(d, e, a, b, c, 37);

    R2(c, d, e, a, b, 38);

    R2(b, c, d, e, a, 39);

    R3(a, b, c, d, e, 40);

    R3(e, a, b, c, d, 41);

    R3(d, e, a, b, c, 42);

    R3(c, d, e, a, b, 43);

    R3(b, c, d, e, a, 44);

    R3(a, b, c, d, e, 45);

    R3(e, a, b, c, d, 46);

    R3(d, e, a, b, c, 47);

    R3(c, d, e, a, b, 48);

    R3(b, c, d, e, a, 49);

    R3(a, b, c, d, e, 50);

    R3(e, a, b, c, d, 51);

    R3(d, e, a, b, c, 52);

    R3(c, d, e, a, b, 53);

    R3(b, c, d, e, a, 54);

    R3(a, b, c, d, e, 55);

    R3(e, a, b, c, d, 56);

    R3(d, e, a, b, c, 57);

    R3(c, d, e, a, b, 58);

    R3(b, c, d, e, a, 59);

    R4(a, b, c, d, e, 60);

    R4(e, a, b, c, d, 61);

    R4(d, e, a, b, c, 62);

    R4(c, d, e, a, b, 63);

    R4(b, c, d, e, a, 64);

    R4(a, b, c, d, e, 65);

    R4(e, a, b, c, d, 66);

    R4(d, e, a, b, c, 67);

    R4(c, d, e, a, b, 68);

    R4(b, c, d, e, a, 69);

    R4(a, b, c, d, e, 70);

    R4(e, a, b, c, d, 71);

    R4(d, e, a, b, c, 72);

    R4(c, d, e, a, b, 73);

    R4(b, c, d, e, a, 74);

    R4(a, b, c, d, e, 75);

    R4(e, a, b, c, d, 76);

    R4(d, e, a, b, c, 77);

    R4(c, d, e, a, b, 78);

    R4(b, c, d, e, a, 79);

    /* Add the working vars back into context.state[] */

    state[0] += a;

    state[1] += b;

    state[2] += c;

    state[3] += d;

    state[4] += e;

    /* Wipe variables */

    a = b = c = d = e = 0;

#ifdef SHA1HANDSOFF

    memset(block, '\0', sizeof(block));

#endif

}

 

 

/* SHA1Init - Initialize new context */

 

void SHA1Init(

    SHA1_CTX* context

)

{

    /* SHA1 initialization constants */

    context->state[0] = 0x67452301;

    context->state[1] = 0xEFCDAB89;

    context->state[2] = 0x98BADCFE;

    context->state[3] = 0x10325476;

    context->state[4] = 0xC3D2E1F0;

    context->count[0] = context->count[1] = 0;

}

 

 

/* Run your data through this. */

 

void SHA1Update(

    SHA1_CTX* context,

    const unsigned char* data,

    uint32_t len

)

{

    uint32_t i;

 

    uint32_t j;

 

    j = context->count[0];

    if ((context->count[0] += len << 3) < j)

        context->count[1]++;

    context->count[1] += (len >> 29);

    j = (j >> 3) & 63;

    if ((j + len) > 63)

    {

        memcpy(&context->buffer[j], data, (i = 64 - j));

        SHA1Transform(context->state, context->buffer);

        for (; i + 63 < len; i += 64)

        {

            SHA1Transform(context->state, &data[i]);

        }

        j = 0;

    }

    else

        i = 0;

    memcpy(&context->buffer[j], &data[i], len - i);

}

 

 

/* Add padding and return the message digest. */

 

void SHA1Final(

    unsigned char digest[20],

    SHA1_CTX* context

)

{

    unsigned i;

 

    unsigned char finalcount[8];

 

    unsigned char c;

 

    for (i = 0; i < 8; i++)

    {

        finalcount[i] = (unsigned char)((context->count[(i >= 4 ? 0 : 1)] >> ((3 - (i & 3)) * 8)) & 255);      /* Endian independent */

    }

 

    c = 0200;

    SHA1Update(context, &c, 1);

    while ((context->count[0] & 504) != 448)

    {

        c = 0000;

        SHA1Update(context, &c, 1);

    }

    SHA1Update(context, finalcount, 8); /* Should cause a SHA1Transform() */

    for (i = 0; i < 20; i++)

    {

        digest[i] = (unsigned char)

            ((context->state[i >> 2] >> ((3 - (i & 3)) * 8)) & 255);

    }

    /* Wipe variables */

    memset(context, '\0', sizeof(*context));

    memset(&finalcount, '\0', sizeof(finalcount));

}