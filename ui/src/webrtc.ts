import { cameraWhep } from './api'

export type StreamSession = {
  connection: RTCPeerConnection
  codec: string | null
  close: () => void
}

export function connectCamera(
  cameraId: string,
  profile: 'main' | 'sub',
  video: HTMLVideoElement,
  profileId?: string
): Promise<StreamSession> {
  return connect(video, (offer) => cameraWhep(cameraId, profile, offer, profileId))
}

async function connect(
  video: HTMLVideoElement,
  negotiate: (sdpOffer: string) => Promise<string>
): Promise<StreamSession> {
  const connection = new RTCPeerConnection({ iceServers: [] })
  const remote = new MediaStream()

  video.srcObject = remote

  connection.addTransceiver('video', { direction: 'recvonly' })
  connection.addTransceiver('audio', { direction: 'recvonly' })

  connection.addEventListener('track', (event) => {
    remote.addTrack(event.track)
    void video.play().catch(() => {
      video.muted = true
      void video.play().catch(() => undefined)
    })
  })

  const offer = await connection.createOffer()
  await connection.setLocalDescription(offer)

  const answer = await negotiate(offer.sdp ?? '')
  await connection.setRemoteDescription({ type: 'answer', sdp: answer })

  return {
    connection,
    codec: extractVideoCodec(answer),
    close: () => {
      connection.close()
      video.srcObject = null
    }
  }
}

export function extractVideoCodec(sdp: string): string | null {
  const match = /a=rtpmap:\d+ (?!rtx|red|ulpfec)(\w+)\//i.exec(sdp)
  return match ? match[1].toUpperCase() : null
}

/** Video codec actually selected by the browser (from WebRTC statistics). */
export async function readVideoCodec(connection: RTCPeerConnection): Promise<string | null> {
  const stats = await connection.getStats()

  let codecId: string | undefined
  stats.forEach((report) => {
    const value = report as { type?: string; kind?: string; codecId?: string }
    if (value.type === 'inbound-rtp' && value.kind === 'video' && value.codecId) {
      codecId = value.codecId
    }
  })

  if (!codecId) {
    return null
  }

  let mimeType: string | undefined
  stats.forEach((report) => {
    const value = report as { id?: string; mimeType?: string }
    if (codecId && value.id === codecId) {
      mimeType = value.mimeType
    }
  })

  return mimeType ? mimeType.split('/')[1] ?? null : null
}
