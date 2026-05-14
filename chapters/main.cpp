#include <iostream>
#include <string>

using namespace std;

void encrypt(string plainText, int key)
{
    cout << "Enter the plain text: ";
    getline(cin, plainText);

    string cipherText = "";

    cout << "Plain Text: " << plainText << endl;

    for (int i = 0; i < plainText.length(); i++)
    {
        char c = plainText[i];
        if (isalpha(c))
        {
            char base = isupper(c) ? 'A' : 'a';
            c = (c - base + key) % 26 + base;
        }
        cipherText += c;
    }

    cout << "Cipher Text: " << cipherText << endl;
}

void decrypt(string cipherText, int key)
{
    cout << "Enter the cipher text: ";
    getline(cin, cipherText);

    string plainText = "";

    cout << "Cipher Text: " << cipherText << endl;

    for (int i = 0; i < cipherText.length(); i++)
    {
        char c = cipherText[i];
        if (isalpha(c))
        {
            char base = isupper(c) ? 'A' : 'a';
            c = (c - base - key + 26) % 26 + base;
        }
        plainText += c;
    }

    cout << "Plain Text: " << plainText << endl;
}

int main()
{

    int k;
    cout << "Ente the key: ";
    cin >> k;
    cin.ignore();

    bool encryption = true; // Set to true for encryption, false for decryption

    cout << "Enter 1 for encryption, 0 for decryption: ";
    cin >> encryption;
    cin.ignore();

    if (encryption)
    {

        string plainText;

        encrypt(plainText, k);
    }
    else
    {
        string cipherText;
        decrypt(cipherText, k);
    }

    return 0;
}