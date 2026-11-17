import { useCallback, useEffect, useRef, useState } from 'react'
import { loadCapturePreferences, type RecordFormat } from '../../capturePreferences'
import { t } from '../../i18n'
import type { ArchivePlaybackController as Controller } from './useArchivePlayback'

export type ArchiveCapture = {
  recording: boolean
  seconds: number
  error: string | null
  toggleRecording: () => void
  takeSnapshot: () => void
  clearError: () => void
}

function safeName(value: string): string {
  return value.replace(/[\\/:*?"<>|]+/g, '_').trim() || 'archive'
}

function stamp(date: Date): string {
  const pad = (value: number) => value.toString().padStart(2, '0')
  return `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}-${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

function pickMime(format: RecordFormat): string | undefined {
  const candidates =
    format === 'mp4'
      ? [
          'video/mp4;codecs=avc1.42E01E,mp4a.40.2',
          'video/mp4',
          'video/webm;codecs=vp9,opus',
          'video/webm'
        ]
      : [
          'video/webm;codecs=vp9,opus',
          'video/webm;codecs=vp8,opus',
          'video/webm',
          'video/mp4'
        ]

  return candidates.find((candidate) => MediaRecorder.isTypeSupported(candidate))
}

/** Стоп-кадр и запись браузером для одного тайла архива. */
export function useArchiveCapture(
  controller: Controller,
  cameraName: string
): ArchiveCapture {
  const recorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const streamRef = useRef<MediaStream | null>(null)
  const [recording, setRecording] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!recording) {
      return
    }

    const timer = window.setInterval(() => setSeconds((value) => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [recording])

  useEffect(
    () => () => {
      recorderRef.current?.stop()
      streamRef.current?.getTracks().forEach((track) => track.stop())
    },
    []
  )

  const startRecording = useCallback(() => {
    const video = controller.video as
      | (HTMLVideoElement & { captureStream?: () => MediaStream })
      | null

    if (!video || typeof video.captureStream !== 'function' || typeof MediaRecorder === 'undefined') {
      setError(t('archive.record.unsupported'))
      return
    }

    const stream = video.captureStream()

    if (stream.getVideoTracks().length === 0) {
      stream.getTracks().forEach((track) => track.stop())
      setError(t('archive.record.unsupported'))
      return
    }

    const mimeType = pickMime(loadCapturePreferences().recordFormat)

    try {
      const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined)
      chunksRef.current = []
      streamRef.current = stream

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          chunksRef.current.push(event.data)
        }
      }

      recorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: recorder.mimeType || 'video/webm' })
        const extension = (recorder.mimeType || blob.type).includes('mp4') ? 'mp4' : 'webm'
        chunksRef.current = []
        stream.getTracks().forEach((track) => track.stop())
        streamRef.current = null
        recorderRef.current = null

        if (blob.size > 0) {
          downloadBlob(blob, `${safeName(cameraName)}-${stamp(new Date())}.${extension}`)
        }
      }

      recorder.start(1000)
      recorderRef.current = recorder
      setSeconds(0)
      setRecording(true)
      setError(null)
    } catch (recordError) {
      stream.getTracks().forEach((track) => track.stop())
      setError(recordError instanceof Error ? recordError.message : String(recordError))
    }
  }, [cameraName, controller])

  const stopRecording = useCallback(() => {
    recorderRef.current?.stop()
    setRecording(false)
  }, [])

  const toggleRecording = useCallback(() => {
    if (recorderRef.current) {
      stopRecording()
      return
    }

    startRecording()
  }, [startRecording, stopRecording])

  const takeSnapshot = useCallback(() => {
    const video = controller.video

    if (!video || video.videoWidth === 0 || video.videoHeight === 0) {
      setError(t('archive.screenshot.empty'))
      return
    }

    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    const context = canvas.getContext('2d')

    if (!context) {
      setError(t('archive.screenshot.failed'))
      return
    }

    context.drawImage(video, 0, 0, canvas.width, canvas.height)

    const format = loadCapturePreferences().snapshotFormat
    const mimeType = format === 'jpeg' ? 'image/jpeg' : 'image/png'
    const extension = format === 'jpeg' ? 'jpg' : 'png'

    canvas.toBlob(
      (blob) => {
        if (!blob) {
          setError(t('archive.screenshot.failed'))
          return
        }

        downloadBlob(
          blob,
          `${safeName(cameraName)}-${stamp(controller.wallTime ?? new Date())}.${extension}`
        )
      },
      mimeType,
      format === 'jpeg' ? 0.92 : undefined
    )

    setError(null)
  }, [cameraName, controller])

  const clearError = useCallback(() => setError(null), [])

  return { recording, seconds, error, toggleRecording, takeSnapshot, clearError }
}
