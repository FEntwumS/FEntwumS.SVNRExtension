using FEntwumS.SVNRExtension.Sbdp.Constants;
using FEntwumS.SVNRExtension.Sbdp.Entities;

namespace FEntwumS.SVNRExtension.Sbdp;

public interface ISbdpTransport : IDisposable
{ 
    void Send(SbdpPacket packet); // sendet einen frame
    
    void SendRaw(ReadOnlySpan<byte> data); // fertig gerahmte bytes 
    
    SbdpPacket? Receive(); // returns null , wenn innerhalb des Timeouts nichts oder zu wenig ankam.
    
    void Flush(); // Verwirft, was noch in den Puffern steht. Noetig vor einer neuen Sitzung
}