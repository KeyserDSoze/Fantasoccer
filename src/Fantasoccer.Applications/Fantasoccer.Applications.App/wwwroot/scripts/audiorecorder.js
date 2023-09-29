var BlazorAudioRecorder = {};

const blobToBase64 = async blob => {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result)
        reader.error = (err) => reject(err)
        reader.readAsDataURL(blob)
    })
}

const convertBlobURLToBase64 = async (url) => {
    const response = await fetch(url)
    const blob = await response.blob();
    const imageBase64 = await blobToBase64(blob)
    return imageBase64;
}
(function () {
    let mStream;
    let mAudioChunks;
    let mMediaRecorder;
    let mCaller;

    BlazorAudioRecorder.Initialize = function (vCaller) {
        mCaller = vCaller;
    };

    BlazorAudioRecorder.StartRecord = async function () {
        mStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        mMediaRecorder = new MediaRecorder(mStream);
        mMediaRecorder.addEventListener('dataavailable', vEvent => {
            mAudioChunks.push(vEvent.data);
        });

        mMediaRecorder.addEventListener('error', vError => {
            console.warn('media recorder error: ' + vError);
        });

        mMediaRecorder.addEventListener('stop', async () => {
            const pAudioBlob = new Blob(mAudioChunks, { type: "audio/mp3;" });
            const pAudioUrl = URL.createObjectURL(pAudioBlob);
            const audioBlobAsBase64 = await convertBlobURLToBase64(pAudioUrl);
            mCaller.invokeMethodAsync('OnAudioUrl', audioBlobAsBase64);
        });

        mAudioChunks = [];
        mMediaRecorder.start();
    };

    BlazorAudioRecorder.PauseRecord = function () {
        mMediaRecorder.pause();
    };

    BlazorAudioRecorder.ResumeRecord = function () {
        mMediaRecorder.resume();
    };

    BlazorAudioRecorder.StopRecord = function () {
        mMediaRecorder.stop();
        mStream.getTracks().forEach(pTrack => pTrack.stop());
    };
})();