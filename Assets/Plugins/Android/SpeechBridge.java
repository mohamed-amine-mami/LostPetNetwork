package com.lostpetnetwork.speech;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;

/**
 * Turns speech into text with the phone's own recogniser (SpeechRecognizer). The answer goes back to the Unity
 * object "SpeechBridge": OnSpeechPartial(text) while talking, OnSpeechResult(text) at the end and
 * OnSpeechError(code) when it fails (the numbers are SpeechRecognizer.ERROR_* values).
 */
public class SpeechBridge {
    private static final String TARGET = "SpeechBridge";
    private static SpeechRecognizer recognizer;

    public static boolean isAvailable(Activity activity) {
        return SpeechRecognizer.isRecognitionAvailable(activity);
    }

    public static void start(final Activity activity, final String languageTag) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    destroyRecognizer();
                    recognizer = SpeechRecognizer.createSpeechRecognizer(activity);
                    recognizer.setRecognitionListener(new Listener());

                    Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
                    intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
                    intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
                    intent.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, activity.getPackageName());
                    if (languageTag != null && languageTag.length() > 0) {
                        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageTag);
                    }
                    recognizer.startListening(intent);
                } catch (Throwable t) {
                    UnityPlayer.UnitySendMessage(TARGET, "OnSpeechError", "5");
                }
            }
        });
    }

    /** Stops listening; the text heard so far still arrives as the final result. */
    public static void stop(final Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (recognizer != null) recognizer.stopListening();
            }
        });
    }

    public static void cancel(final Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                destroyRecognizer();
            }
        });
    }

    private static void destroyRecognizer() {
        if (recognizer != null) {
            try {
                recognizer.cancel();
                recognizer.destroy();
            } catch (Throwable ignored) {
                // already gone
            }
            recognizer = null;
        }
    }

    private static String first(Bundle results) {
        if (results == null) return "";
        ArrayList<String> list = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
        return (list == null || list.isEmpty()) ? "" : list.get(0);
    }

    private static class Listener implements RecognitionListener {
        @Override public void onReadyForSpeech(Bundle params) { }
        @Override public void onBeginningOfSpeech() { }
        @Override public void onRmsChanged(float rmsdB) { }
        @Override public void onBufferReceived(byte[] buffer) { }
        @Override public void onEndOfSpeech() { }
        @Override public void onEvent(int eventType, Bundle params) { }

        @Override
        public void onPartialResults(Bundle partialResults) {
            String text = first(partialResults);
            if (text.length() > 0) UnityPlayer.UnitySendMessage(TARGET, "OnSpeechPartial", text);
        }

        @Override
        public void onResults(Bundle results) {
            UnityPlayer.UnitySendMessage(TARGET, "OnSpeechResult", first(results));
            destroyRecognizer();
        }

        @Override
        public void onError(int error) {
            UnityPlayer.UnitySendMessage(TARGET, "OnSpeechError", String.valueOf(error));
            destroyRecognizer();
        }
    }
}
